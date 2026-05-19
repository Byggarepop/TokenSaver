public interface IShape { string Name(); }

public class Circle : IShape
{
    public string Name() => "Circle";
}
