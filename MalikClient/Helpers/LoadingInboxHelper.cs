using System.Collections.ObjectModel;

namespace MalikClient.Helpers
{
    public static class ObservableCollectionExtensions
    {
        public static void ReplaceAll<T>(this ObservableCollection<T> collection, IReadOnlyList<T> newItems)
        {
            collection.Clear();
            for (int i = 0; i < newItems.Count; i++)
            {
                collection.Add(newItems[i]);
            }
        }
    }
}
