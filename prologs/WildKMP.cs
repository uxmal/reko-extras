namespace prologs;


/**
 * A modified version of the original Knuth-Morris-Pratt substring search algorithm which allows wildcards.
 *
 * @author Varun Shah
 */
public class WildKMP
{

    /**
     * Given some text and a pattern, it searches for the first instance of the pattern in the text.<br>
     * An asterisk (*) in the pattern tells the algorithm to match on any character at that location in the text.
     *
     * @param text    The text to be searched
     * @param pattern The pattern to search for in the text
     * @return The starting index of the pattern in the text. If not found, -1 is returned.
     */
    public static int search(string text, string pattern)
    {

        int textLength = text.Length;
        int patternLength = pattern.Length;
        if (patternLength > textLength)
        {
            return -1;
        }

        // create dfa
        int[] prefixTable = getDFA(pattern);

        int matchLength = 0;
        char? wildLetter = null;
        for (int i = 0; i < textLength; i++)
        {
            // back-track on failure
            while (matchLength > 0 && pattern[matchLength] != text[i])
            {
                // check if fail was due to wildcard
                if (pattern[matchLength] == '*')
                {
                    // if initial wildcard, set it
                    if (wildLetter == null)
                    {
                        wildLetter = text[i];

                        // loop-back with KMP - double check already matched pattern
                        int kmpValue = search(text.Substring(i - matchLength, i),
                                              pattern.Substring(0, matchLength));
                        if (kmpValue != 0)
                        {
                            matchLength = 0; // reset match
                        }
                        else if (pattern[matchLength - 1] == '*')
                        {
                            wildLetter = text[i - 1]; // reset wildcard
                        }
                        break;
                    }
                    else if (wildLetter == text[i])
                    {
                        break; // wildcard matches
                    }
                }

                matchLength = prefixTable[matchLength - 1]; // fall-back
                wildLetter = null;

                // edge case - match previous seen for proper shift
                if (matchLength == 0 && pattern[matchLength + 1] == '*'
                        && text[i - 1] == pattern[matchLength])
                {
                    matchLength++;
                }
            }

            // match or wildcard
            if (pattern[matchLength] == text[i] || pattern[matchLength] == '*')
            {
                // wildcard
                if (pattern[matchLength] == '*')
                {
                    if (wildLetter == null)
                    {
                        wildLetter = text[i]; // set wildcard
                    }
                    else if (wildLetter != text[i])
                    {
                        // doesn't match current wildcard
                        if (matchLength == 1)
                        {
                            wildLetter = text[i]; // edge case, new wildcard
                            continue;
                        }
                        // reset
                        wildLetter = null;
                        matchLength = 0;
                        continue;
                    }
                }
                else
                {
                    wildLetter = null; // reset wildcard
                }
                matchLength++; // matched
            }

            // found the pattern
            if (matchLength == patternLength)
            {
                return i - (patternLength - 1);
            }
        }

        // couldn't find the pattern
        return -1;
    }

    /**
     * Creates the DFA for the KMP algorithm.
     *
     * @param pattern The pattern which is being searched in the text
     * @return The DFA.
     */
    private static int[] getDFA(String pattern)
    {
        int length = pattern.Length;
        int[] dfa = new int[length];
        dfa[0] = 0;
        int longestPrefixIndex = 0;

        for (int i = 2; i < length; i++)
        {
            // back-track
            while (longestPrefixIndex > 0 && pattern[longestPrefixIndex + 1] != pattern[i])
            {
                longestPrefixIndex = dfa[longestPrefixIndex];
            }

            // match
            if (pattern[longestPrefixIndex + 1] == pattern[i])
            {
                longestPrefixIndex++;
            }
            dfa[i] = longestPrefixIndex;
        }
        return dfa;
    }

    public static void main(String[] args)
    {

        System.Console.WriteLine("Enter the text: ");
        String text = Console.ReadLine() ?? "";
        System.Console.WriteLine("Enter the pattern: ");
        String pattern = Console.ReadLine() ?? "";

        int index = search(text, pattern);
        if (index != -1)
        {
            System.Console.WriteLine(index);
        }
        else
        {
            System.Console.WriteLine("Couldn't find pattern in the text.");
        }
    }
}
