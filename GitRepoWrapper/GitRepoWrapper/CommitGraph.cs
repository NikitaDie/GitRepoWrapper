using IntervalTree;

namespace GitRepoWrapper
{
    public enum NodeType
    {
        Commit,
        Stash
    }

    public class Node
    {
        public int I { get; set; }
        public int J { get; set; }
        public NodeType Type { get; set; }

        public Node(int i, int j, NodeType type)
        {
            I = i;
            J = j;
            Type = type;
        }

        public override string ToString()
        {
            return $"{I}|{J}";
        }
    }

    public enum EdgeType
    {
        Normal,
        Merge
    }

    public class Edge
    {
        public Node Source { get; set; }
        public Node Target { get; set; }
        public EdgeType Type { get; set; }

        public Edge(Node source, Node target, EdgeType type) 
        { 
            Source = source; 
            Target = target; 
            Type = type;
        }

        public override string ToString()
        {
            return $"{Source}, {Target}, {Type}";
        }
    }

    public class CommitGraph
    {
        public Dictionary<string, Node> Positions { get; set; }
        public int Width { get; set; }
        public IntervalTree<int, Edge> Edges { get; set; }

        public CommitGraph()
        {
            this.Positions = new Dictionary<string, Node>();
            Width = 0;
            this.Edges = new IntervalTree<int, Edge>(); //TODO: unnecessary initialisation
        }

        public void ComputePositions(RepoWrapper repo)
        {
            this.Positions.Clear();
            repo.Commits.ForEach(commit => Positions[commit.Sha] = new Node(0, 0, NodeType.Commit)); //?
            string? headSha = repo.HeadCommit?.Sha; //const?
            int i = 0;
            List<string?> branches = new List<string?>()
            {
                 "index"
            };
            var activeNodes = new Dictionary<string, HashSet<int>>();
            PriorityQueue<string, int> activeNodesQueue =
                new PriorityQueue<string, int>(Comparer<int>.Create((lhs, rhs) => lhs - rhs));
            //var activeNodesQueue = new Queue<int, string>(lhs => lhs.Item1 < rhs.Item1);
            activeNodes["index"] = new HashSet<int>();
            if (headSha != null)
            {
                activeNodesQueue.Enqueue("index", repo.ShaToIndex[headSha]);
            }

            foreach (var commit in repo.Commits)
            {
                int j = -1;
                var commitSha = commit.Sha;
                List<string> children = repo.Children[commitSha];
                var branchChildren = children.Where(childSha => repo.Parents[childSha][0] == commitSha).ToList();
                var mergeChildren = children.Where(childSha => repo.Parents[childSha][0] != commitSha).ToList();

                //if (commitSha == "6e5e1a96daf3ceeca21bd6f31fbcc8e41ed387e4")
                //{
                //    j = -1;
                //}

                //Compute forbidden indices
                var forbiddenIndices = GetForbiddenIndices(mergeChildren, activeNodes); //+

                //Find a commit to replace
                var commitToReplace = FindCommitToReplace(commitSha, headSha, branchChildren, forbiddenIndices);

                //Insert the commit in the active branches
                if (commitToReplace.Item1 != null)
                {
                    j = commitToReplace.Item2;
                    branches[j] = commitSha;
                }
                else
                {
                    if (children.Count > 0)
                    {
                        var childSha = children[0];
                        var jChild = Positions[childSha].J;
                        j = InsertCommit(commitSha, jChild, forbiddenIndices, branches);
                    }
                    else
                    {
                        j = InsertCommit(commitSha, 0, new HashSet<int>(), branches);
                    }
                }

                //Remove useless active nodes
                RemoveUselessActiveNodes(ref activeNodesQueue, ref activeNodes, i); //?

                //Update the active nodes
                UpdateActiveNodes(ref activeNodes, ref activeNodesQueue, j, branchChildren, commitSha, repo);

                //Remove children from active branches
                RemoveChildrenFromActiveBranches(ref branches, branchChildren, commitToReplace.Item1);

                //If the commit has no parent, remove it from active branches
                if (repo.Parents[commitSha].Count == 0)
                {
                    branches[j] = null;
                }

                //Finally set the position
                SetCommitPosition(commitSha, i, j, repo);
                ++i;
            }

            Width = branches.Count;
            UpdateIntervalTree(repo);
        }

        private HashSet<int> GetForbiddenIndices(List<string> mergeChildren,
            Dictionary<string, HashSet<int>> activeNodes)
        {
            //Compute forbidden indices
            string? highestChild = null;
            int iMin = int.MaxValue;

            foreach (var childSha in mergeChildren)
            {
                var iChild = Positions[childSha].I;
                if (iChild < iMin)
                {
                    iMin = iChild;
                    highestChild = childSha;
                }
            }

            return highestChild != null ? activeNodes[highestChild] : new HashSet<int>();
        }

        private (string?, int) FindCommitToReplace(in string commitSha, string? headSha, List<string> branchChildren,
            HashSet<int> forbiddenIndices)
        {
            string? commitToReplace = null;
            int jCommitToReplace = int.MaxValue;

            if (commitSha == headSha)
            {
                commitToReplace = "index";
                jCommitToReplace = 0;
            }
            else
            {
                //The commit can only replace a child whose first parent is this commit
                foreach (var childSha in branchChildren)
                {
                    int jChild = Positions[childSha].J; //?
                    if (!forbiddenIndices.Contains(jChild) && jChild < jCommitToReplace)
                    {
                        commitToReplace = childSha;
                        jCommitToReplace = jChild;
                    }
                }
            }

            return (commitToReplace, jCommitToReplace);
        }

        private int InsertCommit(in string commitSha, int j, HashSet<int> forbiddenIndices,
            List<string?> branches) //?? ref //j - 1
        {
            //Try to insert as close as possible to i
            //replace i by j
            int dj = 1;
            while (j - dj >= 0 || j + dj < branches.Count)
            {
                if (j + dj < branches.Count && branches[j + dj] == null && !forbiddenIndices.Contains(j + dj)) //?
                {
                    branches[j + dj] = commitSha;
                    return j + dj;
                }
                else if (j - dj >= 0 && branches[j - dj] == null && !forbiddenIndices.Contains(j - dj)) //?
                {
                    branches[j - dj] = commitSha;
                    return j - dj;
                }

                ++dj;
            }

            //If it is not possible to find an available position, append
            branches.Add(commitSha);
            return branches.Count - 1;
        }

        private void RemoveUselessActiveNodes(ref PriorityQueue<string, int> activeNodesQueue,
            ref Dictionary<string, HashSet<int>> activeNodes, int i) //????????????
        {
            activeNodesQueue.TryPeek(out _, out var activeNodesI); //TODO: optimization

            while (activeNodesQueue.Count > 0 && activeNodesI < i) //?
            {
                var sha = activeNodesQueue.Dequeue();
                activeNodes.Remove(sha);
                activeNodesQueue.TryPeek(out _, out activeNodesI);
            }
        }

        private void UpdateActiveNodes(ref Dictionary<string, HashSet<int>> activeNodes,
            ref PriorityQueue<string, int> activeNodesQueue, int j, List<string> branchChildren, string commitSha,
            RepoWrapper repo) //? //smt goes wrong here
        {
            var jToAdd = new List<int>() { j };
            jToAdd.AddRange(branchChildren.Select(childSha => Positions[childSha].J));

            foreach (var activeNode in activeNodes.Values)
            {
                jToAdd.ForEach(jValue => activeNode.Add(jValue));
            }

            activeNodes[commitSha] = new HashSet<int>();

            int iRemove; //TODO: doesn`t work with the first commit, that doesn`t have a parent
            try
            {
                iRemove = repo.Parents[commitSha].Select(parentSha => repo.ShaToIndex[parentSha]).Max();
            }
            catch
            {
                return;
            }

            activeNodesQueue.Enqueue(commitSha, iRemove);
        }

        private void RemoveChildrenFromActiveBranches(ref List<string?> branches, List<string> branchChildren,
            string? commitToReplace)
        {
            foreach (string? childSha in branchChildren)
            {
                if (childSha != commitToReplace)
                {
                    branches[this.Positions[childSha].J] = null; //?
                }
            }
        }

        private void SetCommitPosition(in string commitSha, int i, int j, in RepoWrapper repo) //in?
        {
            this.Positions[commitSha] =
                new Node(i, j, repo.Stashes.ContainsKey(commitSha) ? NodeType.Stash : NodeType.Commit);
            //this.positions.Add(commitSha, new Node(i, j, repo.Stashes.ContainsKey(commitSha) ? NodeType.Stash : NodeType.Commit)); // this.positions.set(commitSha, [i, j, repo.stashes.has(commitSha) ? NodeType.Stash : NodeType.Commit]);
        }

        private void UpdateIntervalTree(RepoWrapper repo)
        {
            this.Edges = new IntervalTree<int, Edge>();

            foreach (var keyValuePair in this.Positions)
            {
                var parents = repo.Parents[keyValuePair.Key];
                foreach (var parent in parents)
                {
                    int i = parents.IndexOf(parent);
                    var parenPosition = this.Positions[parent];
                    var newEdge = new Edge(keyValuePair.Value, parenPosition, i > 0 ? EdgeType.Merge : EdgeType.Normal);
                    this.Edges.Add(keyValuePair.Value.I, parenPosition.I, newEdge);
                }
            }
        }
    }
}
