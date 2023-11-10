namespace GitRepoWrapper
{
    internal class Coordinates
    {
        public int I { get; set; } = 0;
        public int J { get; set; } = 0;
        public State CommitState { get; set; } = State.None;

        public enum State
        {
            None,
        }
    }
}
