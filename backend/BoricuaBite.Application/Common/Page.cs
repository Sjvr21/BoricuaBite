namespace BoricuaBite.Application.Common;

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record PageRequest
{
    public int Number { get; }
    public int Size { get; }
    public int Offset => (Number - 1) * Size;

    public PageRequest(int number = 1, int size = 20)
    {
        if (number < 1 || number > 10000) throw new ArgumentOutOfRangeException(nameof(number));
        if (size < 1 || size > 100) throw new ArgumentOutOfRangeException(nameof(size));
        Number = number;
        Size = size;
    }
}
