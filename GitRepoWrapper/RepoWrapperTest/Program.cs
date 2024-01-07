// See https://aka.ms/new-console-template for more information

using GitRepoWrapper;
using LibGit2Sharp;

Console.WriteLine("Hello, World!");

//var repoTree = new CommitsTree(@"Q:\C#\electron-api-demos\.git");
//var repoTree = new RepoWrapper(new Repository(@"Q:\C#\MyGAL\.git"));
var repoTree = new RepoWrapper(new Repository(@"Q:\C#\electron-api-demos\.git"));
Console.WriteLine(repoTree.Name);
repoTree.Init();
foreach (var keyValuePair in repoTree.Graph.Positions)
{
    //Console.WriteLine($"{repoTree.ShaToCommit[keyValuePair.Key].Message}: {keyValuePair.Value.I}|{keyValuePair.Value.J}");
    Console.WriteLine($"{keyValuePair.Key}: {keyValuePair.Value.I}|{keyValuePair.Value.J}, {keyValuePair.Value.Type}");
}

foreach (var keyValuePair in repoTree.Graph.Edges)
{
    //Console.WriteLine($"{repoTree.ShaToCommit[keyValuePair.Key].Message}: {keyValuePair.Value.I}|{keyValuePair.Value.J}");
    Console.WriteLine($"{keyValuePair.Value}");
}