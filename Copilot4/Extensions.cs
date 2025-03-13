namespace Copilot4;

static class Extensions {
    public static TElement? ValueAt<TElement> ( this IList<TElement> list, int index ) {
        if (index >= list.Count || index < 0) {
            return default;
        }

        return list [ index ];
    }
}
