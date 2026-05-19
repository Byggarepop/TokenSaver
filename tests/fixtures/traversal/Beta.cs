public class Square : IShape
{
    public string Name() => "Square";
}

public class Drawer
{
    public void Draw(IShape s) => Console.WriteLine(s.Name());
}
