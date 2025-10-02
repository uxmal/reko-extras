using Reko.Core;

namespace prologs;

/// <summary>
/// Represents a memory block. This is not the "Segment" in ELF memory model
/// </summary>
public class Segment
{
    public Address start { get; internal set; }
    public Address end { get; internal set; }
    public string sort { get; }

    /// <summary>
    /// </summary>
    /// <param name="start">Start addresss.</param>
    /// <param name="end">End address.</param>
    /// <param name="sort">Type of the segment, can be code, data, etc.</param>
    /// <param name=""></param>
    /// <returns></returns>
    public Segment(Address start, Address end, string sort)
    {
        this.start = start;
        this.end = end;
        this.sort = sort;
    }

    public override string ToString()
    {
        var s = $"[{this.start:X}-{this.end:X}, {this.sort}]";
        return s;
    }

    /// <summary>
    /// Calculate the size of the Segment.
    /// </summary>
    /// <returns>Size of the Segment</returns>
    public long size => this.end - this.start;

    /// <summary>
    /// Make a copy of the Segment.
    /// </summary>
    /// <returns>A copy of the Segment instance.</returns>
    public Segment copy()
    {
        return new Segment(this.start, this.end, this.sort);
    }
}


/// <summary>
/// SegmentList describes a series of segmented memory blocks. You may query whether an address belongs to any of the
/// blocks or not, && obtain the exact block(segment) that the address belongs to.
/// </summary>
public class SegmentList
{
    private List<Segment> _list;
    private long _bytes_occupied;

    private static logging.Logger l = logging.getLogger(name: nameof(SegmentList));

    public SegmentList() {
        this._list = [];
        this._bytes_occupied = 0;
    }

    //
    // Overridden methods
    //

    public int Count => this._list.Count;

    public Segment this[int idx] => this._list[idx];

    //
    // Private methods
    //


    /// <summary>
    /// Determines whether the block specified by (address, size) should be merged with adjacent blocks.
    /// <param name="address">Starting address of the block to be merged.</param>
    /// <param name="size"> Size of the block to be merged.</param>
    /// <param name="sort"> Type of the block.</param>
    /// <param name="idx"> ID of the address.</param>
    /// </summary>
    private void _insert_and_merge(Address address, long size, string sort, int idx)
    {
        // sanity check
        if (idx > 0 && address + size <= this._list[idx - 1].start) {
            // There is a bug, since _list[idx] must be the closest one that is less than the current segment
            l.warning("BUG FOUND: new segment should always be greater than _list[idx].");
            // Anyways, let's fix it.
            this._insert_and_merge(address, size, sort, idx - 1);
            return;
        }

        // Insert the block first
        // The new block might be overlapping with other blocks. _insert_and_merge_core will fix the overlapping.
        if (idx == this._list.Count)
            this._list.Add(new Segment(address, address + size, sort));
        else
            this._list.Insert(idx, new Segment(address, address + size, sort));
        // Apparently _bytes_occupied will be wrong if the new block overlaps with any existing block. We will fix it
        // later
        this._bytes_occupied += size;

        // Search forward to merge blocks if necessary
        int pos = idx;
        while (pos < this._list.Count) {
            var (merged, p, bytes_change) = this._insert_and_merge_core(pos, "forward");
            pos = p;
            if (!merged)
                break;

            this._bytes_occupied += bytes_change;
        }

        // Search backward to merge blocks if necessary
        pos = idx;

        while (pos > 0) {
            var (merged, p, bytes_change) = this._insert_and_merge_core(pos, "backward");
            pos = p;
            if (!merged)
                break;

            this._bytes_occupied += bytes_change;
        }
    }

    /// <summary>
    ///     The core part of method _insert_and_merge.
    /// </summary>
    /// <param name="pos">The starting position.</param>
    /// <param name="direction">If we are traversing forwards or backwards in the list. It determines where the "sort"
    ///    of the overlapping memory block comes from.If everything works as expected, "sort" of
    /// the overlapping block is always equal to the segment occupied most recently.</param>
    /// <returns>A tuple of (merged (bool), new position to begin searching (int), change in total bytes (int)</returns>
    private (bool, int, long) _insert_and_merge_core(int pos, string direction) {

        long bytes_changed = 0;

        Segment previous_segment;
        int previous_segment_pos;
        Segment segment;
        int segment_pos;
        if (direction == "forward") {
            if (pos == this._list.Count - 1)
                return (false, pos, 0);
            previous_segment = this._list[pos];
            previous_segment_pos = pos;
            segment = this._list[pos + 1];
            segment_pos = pos + 1;
        } else {  // if direction == "backward":
            if (pos == 0)
                return (false, pos, 0);
            segment = this._list[pos];
            segment_pos = pos;
            previous_segment = this._list[pos - 1];
            previous_segment_pos = pos - 1;
        }
        bool merged = false;
        int new_pos = pos;

        if (segment.start <= previous_segment.end) {
            // we should always have new_start+new_size >= segment.start

            if (segment.sort == previous_segment.sort) {
                // They are of the same sort - we should merge them!
                var new_end = Address.Max(previous_segment.end, segment.start + segment.size);
                var new_start = Address.Min(previous_segment.start, segment.start);
                var new_size = new_end - new_start;
                this._list[segment_pos] = new Segment(new_start, new_end, segment.sort);
                this._list.RemoveAt(previous_segment_pos);
                bytes_changed = -(segment.size + previous_segment.size - new_size);

                merged = true;
                new_pos = previous_segment_pos;
            }
            else {
                // Different sorts. It's a bit trickier.
                if (segment.start == previous_segment.end) {
                    // They are adjacent. Just don't merge.
                    //pass
                } else {
                    // They are overlapping. We will create one, two, or three different blocks based on how they are
                    // overlapping
                    List<Segment> new_segments = [];
                    if (segment.start < previous_segment.start) {
                        new_segments.Add(new Segment(segment.start, previous_segment.start, segment.sort));

                        var sort = direction == "forward" ? previous_segment.sort : segment.sort;
                        new_segments.Add(new Segment(previous_segment.start, previous_segment.end, sort));

                        if (segment.end < previous_segment.end)
                            new_segments.Add(new Segment(segment.end, previous_segment.end, previous_segment.sort));
                        else if (segment.end > previous_segment.end)
                            new_segments.Add(new Segment(previous_segment.end, segment.end, segment.sort));
                    } else {   // segment.start >= previous_segment.start
                        if (segment.start > previous_segment.start)
                            new_segments.Add(new Segment(previous_segment.start, segment.start, previous_segment.sort));
                        var sort = direction == "forward" ? previous_segment.sort : segment.sort;
                        if (segment.end > previous_segment.end) {
                            new_segments.Add(new Segment(segment.start, previous_segment.end, sort));
                            new_segments.Add(new Segment(previous_segment.end, segment.end, segment.sort));
                        } else if (segment.end < previous_segment.end) {
                            new_segments.Add(new Segment(segment.start, segment.end, sort));
                            new_segments.Add(new Segment(segment.end, previous_segment.end, previous_segment.sort));
                        } else {
                            new_segments.Add(new Segment(segment.start, segment.end, sort));
                        }
                    }
                    // merge segments in new_segments array if they are of the same sort
                    int i = 0;
                    while (new_segments.Count > 1 && i < new_segments.Count - 1) {
                        var s0 = new_segments[i];
                        var s1 = new_segments[i + 1];
                        if (s0.sort == s1.sort) {
                            List<Segment> ns = [];
                            ns.AddRange(new_segments.Take(i));
                            ns.Add(new Segment(s0.start, s1.end, s0.sort));
                            ns.AddRange(new_segments.Skip(i + 2));
                            new_segments = ns;
                        } else
                            ++i;
                    }
                    // Put new segments into this._list
                    var old_size = this._list[previous_segment_pos..(segment_pos + 1)].Sum(seg => seg.size);
                    var new_size = new_segments.Sum(seg => seg.size);
                    bytes_changed = new_size - old_size;

                    var nl = new List<Segment>();
                    nl.AddRange(_list[0..previous_segment_pos]);
                    nl.AddRange(new_segments);
                    nl.AddRange(this._list[(segment_pos + 1)..]);

                    merged = true;

                    if (direction == "forward")
                        new_pos = previous_segment_pos + new_segments.Count - 1;
                    else
                        new_pos = previous_segment_pos;
                }
            }
        }
        return (merged, new_pos, bytes_changed);
    }

    private void _remove(Address init_address, long init_size, int init_idx)
    {
        var address = init_address;
        var size = init_size;
        var idx = init_idx;

        while (idx < this._list.Count) {
            var segment = this._list[idx];
            if (segment.start <= address) {
                if (address < segment.start + segment.size &&
                    segment.start + segment.size < address + size) {
                    // |---segment---|
                    //      |---address + size---|
                    // shrink segment
                    segment.end = address;
                    // adjust address
                    var new_address = segment.start + segment.size;
                    // adjust size
                    size = address + size - new_address;
                    address = new_address;
                    // update idx
                    idx = this.search(address);
                } else if (address < segment.start + segment.size && address + size <= segment.start + segment.size) {
                    // |--------segment--------|
                    //    |--address + size--|
                    // break segment
                    var seg0 = new Segment(segment.start, address, segment.sort);
                    var seg1 = new Segment(address + size, segment.start + segment.size, segment.sort);
                    // remove the current segment
                    this._list.RemoveAt(idx);
                    if (seg1.size > 0)
                        this._list.Insert(idx, seg1);
                    if (seg0.size > 0)
                        this._list.Insert(idx, seg0);
                    // done
                    break;
                } else
                    throw new Exception("Unreachable reached");
            } else { // if segment.start > address
                if (address + size <= segment.start) {
                    //                      |--- segment ---|
                    // |-- address + size --|
                    // no overlap
                    break;
                } else if (segment.start < address + size &&
                           address + size <= segment.start + segment.size) {
                    //            |---- segment ----|
                    // |-- address + size --|
                    //
                    // update the start of the segment
                    segment.start = address + size;
                    if (segment.size == 0)
                        this._list.RemoveAt(idx);
                    break;
                } else if (address + size > segment.start + segment.size) {
                    //            |---- segment ----|
                    // |--------- address + size ----------|
                    this._list.RemoveAt(idx);
                    var new_address = segment.end;
                    size = address + size - new_address;
                    address = new_address;
                    idx = this.search(address);
                } else
                    throw new Exception("Unreachable reached");
            }
        }
    }

    /// <summary>
    /// Returns a string representation of the segments that form this SegmentList
    /// </summary>
    /// <param name=""></param>
    /// <returns>String representation of contents</returns>
    public string _dbg_output() {
        var s = "[";
        List<string> lst = [];
        foreach (var segment in this._list) {
            lst.Add(segment.ToString());
        }
        s += string.Join(", ", lst);
        s += "]";
        return s;
    }

    /// <summary>
    /// Iterates over list checking segments with same sort do not overlap
    /// Raises an exception if segments overlap space with same sort
    /// </summary>
    public void _debug_check()
    {
        // old_start = 0
        Address old_end = new();
        var old_sort = "";
        foreach (var segment in this._list)
        {
            if (segment.start <= old_end && segment.sort == old_sort)
                throw new ApplicationException("Error in SegmentList: blocks are not merged");
            // old_start = start
            old_end = segment.end;
            old_sort = segment.sort;
        }
    }

//
// Public methods
//
    /// <summary>
    /// Checks which segment that the address `addr` should belong to, and, returns the offset of that segment.
    ///  Note that the address may not actually belong to the block.
    /// </summary>
    /// <param name="addr">The address to search.</param>
    /// <returns>The offset of the segment.</returns>
    public int search(Address addr) {

        int start = 0;
        int end = this._list.Count;

        while (start != end) {
            int mid = start + (end - start) / 2;

            var segment = this._list[mid];
            if (addr < segment.start)
                end = mid;
            else if (addr >= segment.end)
                start = mid + 1;
            else {
                // Overlapped :(
                start = mid;
                break;
            }
        }
        return start;
    }

    /// <summary>
    /// Returns the next free position with respect to an address, including that address itself
    /// </summary>
    /// <param name="address">The address to begin the search with (including itself)</param>
    /// <returns>The next free position</returns>
    public Address next_free_pos(Address address) {

        int idx = this.search(address);
        if (idx < this._list.Count && this._list[idx].start <= address && address < this._list[idx].end) {
            // Occupied
            int i = idx;
            while (i + 1 < this._list.Count && this._list[i].end == this._list[i + 1].start)
                ++i;
            if (i == this._list.Count)
                return this._list[-1].end;

            return this._list[i].end;
        }
        return address;
    }

    /// <summary>
    /// Returns the address of the next occupied block whose sort is not one of the specified ones.
    /// </summary>
    /// <param name="address"> The address to begin the search with(including itself).</param>
    /// <param name="sorts">A collection of sort strings.</param>
    /// <param name="max_distance">The maximum distance between <paramref name="address"/> and the next position. 
    /// Search will stop after
    /// we come across an occupied position that is beyond <paramref name="address"/> + <paramref name="max_distance"/>.This check
    /// will be disabled if <paramref name="max_distance"/> is set to null.</param>
    /// <returns>
    /// The next occupied position whose sort is not one of the specified ones, or None if no such
    /// position exists.
    /// </returns>
    public Address? next_pos_with_sort_not_in(Address address, IReadOnlyCollection<string> sorts, long? max_distance = null) {

        int list_length = this._list.Count;

        int idx = this.search(address);
        if (idx < list_length) {
            // Occupied
            var block = this._list[idx];

            if (max_distance.HasValue && address + max_distance.Value < block.start)
                return null;

            if (block.start <= address && address < block.end) {
                // the address is inside the current block
                if (!sorts.Contains(block.sort)) {
                    return address;
                }
                idx += 1;
            }
            int i = idx;
            while (i < list_length) {
                if (max_distance.HasValue && address + max_distance.Value < this._list[i].start)
                    return null;
                if (!sorts.Contains(this._list[i].sort))
                    return this._list[i].start;
                ++i;
            }
        }
        return null;
    }


    /// <summary>
    /// Check if an address belongs to any segment
    /// </summary>
    /// <params>The address to check.</params>
    /// <returns>True if this address belongs to a segment, false otherwise.</returns>
    public bool is_occupied(Address address) {

        var idx = this.search(address);
        if (this._list.Count <= idx)
            return false;
        if (this._list[idx].start <= address && address < this._list[idx].end)
            return true;
        if (idx > 0 && address < this._list[idx - 1].end)
            // TODO: It seems that this branch is never reached. Should it be removed?
            return true;
        return false;
    }

    /// <summary>
    /// Check if an address belongs to any segment, and if yes, returns the sort of the segment
    /// </summary>
    /// <param name="address">The address to check</param>
    /// <returns>Sort of the segment that occupies this address</returns>
    public string? occupied_by_sort(Address address) {
        var idx = this.search(address);
        if (this._list.Count <= idx)
            return null;
        if (this._list[idx].start <= address && address < this._list[idx].end)
            return this._list[idx].sort;
        if (idx > 0 && address < this._list[idx - 1].end) {
            // TODO: It seems that this branch is never reached. Should it be removed?
            return this._list[idx - 1].sort;
        }
        return null;
    }

    /// <summary>
    ///         Check if an address belongs to any segment, && if yes, returns the beginning, the size, && the sort of the
    ///    segment.
    /// <param name="address">The address to check</param>
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public (Address, long, string)? occupied_by(Address address) {

        var idx = this.search(address);
        if (this._list.Count <= idx)
            return null;
        if (this._list[idx].start <= address && address < this._list[idx].end) {
            var block = this._list[idx];
            return (block.start, block.size, block.sort);
        }
        if (idx > 0 && address < this._list[idx - 1].end) {
            // TODO: It seems that this branch is never reached. Should it be removed?
            var block = this._list[idx - 1];
            return (block.start, block.size, block.sort);
        }
        return null;
    }

    /// <summary>
    /// Include a block, specified by (address, size), in this segment list.
    /// </summary>
    /// <param name="address">The starting address of the block.</param>
    /// <param name="size">Size of the block.</param>
    /// <param name="sort">Type of the block.</param>
    public void occupy(Address address, long size, string sort) {

        if (size <= 0) {
            // Cannot occupy a non-existent block
            return;
        }

        // l.debug("Occpuying 0x%08x-0x%08x", address, address + size)
        if (this._list.Count == 0) {
            this._list.Add(new Segment(address, address + size, sort));
            this._bytes_occupied += size;
            return;
        }
        // Find adjacent element in our list
        int idx = this.search(address);

        this._insert_and_merge(address, size, sort, idx);

        // this._debug_check()
    }

    /// <summary>
    /// Remove a block, specified by (address, size), in this segment list.
    /// </summary>
    /// <param name="address">The starting address of the block.</param>
    /// <param name="size">Size of the block.</param>
    public void release(Address address, long size) {
        if (size <= 0) {
            // cannot release a non-existent block
            return;
        }
        if (this._list.Count == 0)
            return;

        int idx = this.search(address);
        if (idx < this._list.Count)
            this._remove(address, size, idx);
        // this._debug_check()
    }


    /// <summary>
    /// Make a copy of the SegmentList.
    /// </summary>
    /// <returns>A copy of the SegmentList instance.</returns>
    public SegmentList copy() {
        var n = new SegmentList();

        n._list = this._list.Select(a => a.copy()).ToList();
        n._bytes_occupied = this._bytes_occupied;
        return n;
    }


    //
    // Properties
    //

    /// <summary>
    /// The sum of sizes of all blocks.
    /// </summary>
    public long occupied_size => this._bytes_occupied;

    /// <summary>
    /// Returns if this segment list has any block or not. !is_empty
    /// </summary>
    /// <returns>True if it's not empty, False otherwise</returns>
    public bool has_blocks => this._list.Count > 0;
}
