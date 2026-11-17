using XLua;

namespace Game2
{
    // Uses a value type generated in another assembly.
    [LuaCallCSharp]
    public class Mover
    {
        public Game.Vec2i Move(Game.Vec2i v) { return new Game.Vec2i(v.X + 1, v.Y + 1); }
    }

    [CSharpCallLua]
    public delegate Game.Vec2i MoveOp(Game.Vec2i v, int by);
}
