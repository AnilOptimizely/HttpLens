namespace SampleWebApi.Services;

public class GitHubService(HttpClient http)
{
    public async Task<string> GetUserAsync() =>
        await http.GetStringAsync("https://api.github.com/users/octocat");

    public async Task<string> GetRepoAsync() =>
        await http.GetStringAsync("https://api.github.com/repos/dotnet/runtime");
}
