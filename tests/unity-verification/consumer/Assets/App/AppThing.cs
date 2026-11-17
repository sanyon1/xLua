using XLua;

// Assembly-CSharp: generated into the default folder.
[LuaCallCSharp]
public static class AppThing
{
    public static int Twice(int a) { return a * 2; }
    public static Game.Vec2i Flip(Game.Vec2i v) { return new Game.Vec2i(v.Y, v.X); }
}
