using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RekoSifter
{
	public struct DisposableGCHandle : IDisposable
	{
		private readonly GCHandle gch;

		public DisposableGCHandle(object? value) {
			gch = GCHandle.Alloc(value);
		}

		public DisposableGCHandle(object? value, GCHandleType type) {
			gch = GCHandle.Alloc(value, type);
		}

		public static DisposableGCHandle Pin(object? value) {
			return new DisposableGCHandle(value, GCHandleType.Pinned);
		}

		public object? Target {
			get => gch.Target;
		}

		public IntPtr AddrOfPinnedObject() => gch.AddrOfPinnedObject();

		public void Dispose() {
			gch.Free();
		}
	}
}
