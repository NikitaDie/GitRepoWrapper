namespace GitRepoWrapper
{
    public class Coordinates
    {
        public int I { get; set; } = 0;
        public int J { get; set; } = 0;
        public State CommitState { get; set; } = State.None;

        public Coordinates() {}

        public Coordinates(int i, int j)
        {
            I = i;
            J = j;
        }

        public enum State
        {
            None,
        }
    }
}
