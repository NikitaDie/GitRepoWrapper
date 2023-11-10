using LibGit2Sharp;
using IntervalTree;

namespace GitRepoWrapper
{
    public enum PatchType
    {
        Unstaged,
        Staged,
        Committed
    }

    public enum RepoState
    {
        Cherrypick,
        Commit,
        Merge,
        Rebase,
        Revert
    }

    public class CustomStash
    {
        public int Index { get; set; }
        public Commit Commit { get; set; }

        public CustomStash(int index, Commit commit)
        {
            Index = index;
            Commit = commit;
        }
    }

    public class RepoWrapper
    {
        private Repository Repo { get; set; }
        private string Path { get; set; }
        public string Name { get; set; }
        public List<Commit> Commits { get; set; }
        public Dictionary<string, Commit> ShaToCommit { get; set; } //private
        public Dictionary<string, int> ShaToIndex { get; set; }
        private Dictionary<string, Reference> References { get; set; } //?
        private Dictionary<string, List<string>> ShaToReferences { get; set; } //?
        public Dictionary<string, CustomStash> Stashes { get; set; }
        private Dictionary<string, Tag> Tags { get; set; } // Annotated tags //?
        public Dictionary<string, List<string>> Parents { get; set; }
        public Dictionary<string, List<string>> Children { get; set; }
        private Reference? Head { get; set; }
        public Commit? HeadCommit { get; set; }
        public CommitGraph Graph { get; set; }


        public RepoWrapper(Repository repo)
        {
            Repo = repo;
            Path = repo.Info.Path;
            Name = GetRepoName();
            Commits = new List<Commit>();
            ShaToCommit = new Dictionary<string, Commit>();
            ShaToIndex = new Dictionary<string, int>();
            References = new Dictionary<string, Reference>();
            ShaToReferences = new Dictionary<string, List<string>>();
            Stashes = new Dictionary<string, CustomStash>();
            Tags = new Dictionary<string, Tag>();
            Parents = new Dictionary<string, List<string>>();
            Children = new Dictionary<string, List<string>>();
            Graph = new CommitGraph();
        }

        public void Init()
        {
            UpdateCommits();
            UpdateHead();
            UpdateGraph();
            /*UpdateIndex();
            UpdateIgnore();*/
        }

        public void SortCommits()
        {
            // Sort the commits by date (from newer to older)
            var commitsWithTime = Commits.Select(commit => (commit, commit.Committer.When)).ToList();
            commitsWithTime.Sort((lhs, rhs) => rhs.When.CompareTo(lhs.When));
            Commits = commitsWithTime.Select(item => item.commit).ToList();

            // Topological sort (from parent to children)
            var sortedCommits = new List<Commit>();
            var alreadySeen = new Dictionary<string, bool>();

            foreach (var commit in Commits)
            {
                Dfs(commit);
            }

            Commits = sortedCommits;

            // Update shaToIndex
            ShaToIndex = new Dictionary<string, int>();
            for (int i = 0; i < Commits.Count; i++)
            {
                ShaToIndex[Commits[i].Id.Sha] = i;
            }

            return;

            void Dfs(Commit commit)
            {
                var commitSha = commit.Id.Sha;
                if (alreadySeen.ContainsKey(commitSha))
                {
                    return;
                }

                alreadySeen[commitSha] = true;
                if (!Children.TryGetValue(commitSha, out var children)) //TODO: fix
                {
                    sortedCommits.Add(commit);
                    return;
                }

                foreach (var childSha in children)
                {
                    Dfs(ShaToCommit[childSha]);
                }
                sortedCommits.Add(commit);
            }
        }

        private string GetRepoName()
        {
            var repoName = Repo.Info.WorkingDirectory.Split(System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
            return repoName[^2];
        }

        private void UpdateHead()
        {
            try
            {
                // Check what happens when HEAD is detached
                Head = Repo.Head.Reference;
            }
            catch (Exception e)
            {
                Head = null;
            }
            HeadCommit = Repo.Head.Tip;
        }

        private void UpdateGraph()
        {
            this.Graph.ComputePositions(this);
        }

        /*public async Task UpdateIndex()
        {
            stagedPatches = await GetStagedPatches();
            unstagedPatches = await GetUnstagedPatches();
        }*/

        //public async Task RequestUpdateCommitsAsync()
        //{
        //    // Start an update only if the previous ones have finished
        //    _updateCommitsPromise = _updateCommitsPromise.Then(() => UpdateCommitsAsync());
        //    await _updateCommitsPromise;
        //}

        public void UpdateCommits()
        {
            var referencesToUpdate = GetReferences();
            var stashesToUpdate = UpdateStashes();
            var newCommits = GetNewCommits(referencesToUpdate, stashesToUpdate);
            UpdateTags();
            UpdateShaToReferences();
            GetParents(newCommits);
            //RemoveUnreachableCommits();
            HideStashSecondParents(stashesToUpdate);
            SortCommits();
        }


        private string[] UpdateReferences()
        {
            var references = GetReferences();
            var referencesToUpdate = new List<string>();
            var newReferences = new Dictionary<string, Reference>();

            foreach (var reference in references)
            {
                var name = reference.CanonicalName;
                var commitSha = reference.TargetIdentifier;

                if (!References.ContainsKey(name) || References[name].TargetIdentifier != commitSha)
                {
                    referencesToUpdate.Add(name);
                }

                newReferences[name] = reference;
            }

            References = newReferences;
            return referencesToUpdate.ToArray();
        }

        public IEnumerable<CustomStash> UpdateStashes() // +/-
        {
            var stashes = GetStashes();
            var stashesToUpdate = new List<CustomStash>();

            foreach (var stash in stashes)
            {
                if (!this.Stashes.ContainsKey(stash.Key))
                {
                    stashesToUpdate.Add(stash.Value);
                }
            }

            this.Stashes = stashes;

            return stashesToUpdate;
        }

        public void UpdateTags()
        {
            var tasks = References.Values
                .Where(reference => reference.IsTag && !ShaToCommit.ContainsKey(reference.TargetIdentifier))
                .Select(reference => new KeyValuePair<string, Tag>(reference.CanonicalName, Repo.Tags[reference.TargetIdentifier]));

            Tags = new Dictionary<string, Tag>();
        }

        public void UpdateShaToReferences()
        {
            ShaToReferences.Clear();

            foreach (var kvp in References)
            {
                string name = kvp.Key;
                Reference reference = kvp.Value;

                string commitSha = Tags.TryGetValue(name, out var tag) ?
                    tag.Target.Id.Sha :
                    reference.TargetIdentifier;

                if (!ShaToReferences.ContainsKey(commitSha))
                {
                    ShaToReferences[commitSha] = new List<string>();
                }

                ShaToReferences[commitSha].Add(name);
            }
        }

        void GetParents(List<Commit> commits)
        {
            foreach (var commit in commits)
            {
                Children[commit.Sha] = new List<string>();
            }

            foreach (var commit in commits)
            {
                var commitSha = commit.Sha;
                var parentShas = commit.Parents.Select(p => p.Sha).ToList();
                Parents[commitSha] = parentShas;

                // Update children
                foreach (var parentSha in parentShas)
                {
                    Children[parentSha].Add(commitSha);
                }
            }
        }

        private List<Commit> GetNewCommits(IEnumerable<Reference> references, IEnumerable<CustomStash> stashes)
        {
            var walker = Repo.Commits.QueryBy(new CommitFilter
            {
                IncludeReachableFrom = references,
                ExcludeReachableFrom = this.Commits.Select(c => c.Sha),
                //IncludeStashes = Stashes.Select(s => s.Value.Commit.Id),
            });

            var newCommits = walker.ToList();

            foreach (var commit in newCommits)
            {
                this.Commits.Add(commit);
                this.ShaToCommit[commit.Sha] = commit;
            }

            return newCommits;
        }

        private void RemoveUnreachableCommits() //Hä?
        {
            // Find unreachable commits by doing a DFS
            Dictionary<string, bool> alreadyAdded = new Dictionary<string, bool>();
            List<Commit> frontier = new List<Commit>(GetReferenceCommits());
            frontier.AddRange(Stashes.Values.Select(stash => stash.Commit));

            foreach (var commit in frontier)
            {
                alreadyAdded[commit.Sha] = true;
            }

            while (frontier.Count > 0)
            {
                var commit = frontier.Last();
                frontier.RemoveAt(frontier.Count - 1);
                var commitSha = commit.Sha;

                foreach (var parentSha in Parents[commitSha])
                {
                    if (!alreadyAdded.ContainsKey(parentSha))
                    {
                        alreadyAdded[parentSha] = true;
                        frontier.Add(ShaToCommit[parentSha]);
                    }
                }
            }

            List<Commit> commitsToRemove = new List<Commit>();

            foreach (var commit in Commits)
            {
                if (!alreadyAdded.ContainsKey(commit.Sha))
                {
                    commitsToRemove.Add(commit);
                }
            }

            // Remove them
            foreach (var commit in commitsToRemove)
            {
                RemoveCommit(commit);
            }
        }

        private void HideStashSecondParents(IEnumerable<CustomStash> stashes)
        {
            // Hide the second parent of stash commits
            var parents = stashes.Select(stash => stash.Commit.Parents.ElementAt(1));

            foreach (var parent in parents)
            {
                RemoveCommit(parent);
            }
        }

        private void RemoveCommit(Commit commit)
        {
            var commitSha = commit.Sha;
            Commits.Remove(commit);
            ShaToCommit.Remove(commitSha);


            if (!Parents.TryGetValue(commitSha, out var parents))
                return;

            foreach (var parentSha in parents)
            {
                var parentChildren = Children[parentSha];
                parentChildren.Remove(commitSha);
            }

            Parents.Remove(commitSha);

            if (!Children.TryGetValue(commitSha, out var children))
                return;

            foreach (var childSha in children)
            {
                var childParents = Parents[childSha];
                childParents.Remove(commitSha);
            }

            Children.Remove(commitSha);
        }

        #region References Operation

        public IEnumerable<Reference> GetReferences()
        {
            var references = Repo.Refs.FromGlob("*").Where(reference => reference.CanonicalName != "refs/stash");
            return references;
        }

        public IEnumerable<Commit> GetReferenceCommits() //-?
        {
            return References.Select(reference => GetReferenceCommit(reference.Value));
        }

        public Commit GetReferenceCommit(Reference reference)
        {
            return Tags.TryGetValue(reference.CanonicalName, out var tag) ? ShaToCommit[tag.Target.Id.Sha] : ShaToCommit[reference.TargetIdentifier];
        }

        #endregion

        #region Stash

        private Dictionary<string, CustomStash> GetStashes() //?
        {

            var stashMap = new Dictionary<string, CustomStash>();

            for (var i = 0; i < Repo.Stashes.Count(); ++i)
            {
                var commit = Repo.Stashes[i].Index;
                stashMap.Add(commit.Sha, new CustomStash(i, commit));
            }

            return stashMap;
        }

        #endregion

    }
}
