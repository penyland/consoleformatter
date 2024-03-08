namespace CustomConsoleFormatter;

public static class StringExtensions
{
    public static IEnumerable<int> AllIndexesOf(this string str, char searchChar)
    {
        var minIndex = str.IndexOf(searchChar);
        while (minIndex != -1)
        {
            yield return minIndex;
            minIndex = str.IndexOf(searchChar, minIndex + 1);
        }
    }
}
