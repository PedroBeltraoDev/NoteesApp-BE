using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NotesApp.Api.Tests;

// Pastas de ponta a ponta (pipeline real, JWT, middleware) sobre um banco em memória.
// O índice único por nome não é aplicado pelo provider em memória: a regra é coberta pela checagem do serviço.
public class FolderFlowTests : IClassFixture<InMemoryApiFactory>
{
    private const string Password = "senha-segura-123";

    private readonly InMemoryApiFactory _factory;

    public FolderFlowTests(InMemoryApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> NewUserAsync()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@teste.com", password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return _factory.CreateClient().WithBearer(data.GetProperty("accessToken").GetString()!);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
        => (await JsonAsync(response)).GetProperty("data");

    private static async Task<string?> MessageAsync(HttpResponseMessage response)
        => (await JsonAsync(response)).GetProperty("message").GetString();

    private static async Task<int> CreateFolderAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/folders", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> CreateNoteAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/notes", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync(response);
    }

    private static async Task<JsonElement> FoldersAsync(HttpClient client)
        => await DataAsync(await client.GetAsync("/api/folders"));

    private static JsonElement Find(JsonElement folders, int id)
        => folders.EnumerateArray().Single(f => f.GetProperty("id").GetInt32() == id);

    // ---------- Criar, listar, renomear

    [Fact]
    public async Task Create_ReturnsTheFolderWithZeroNotes_AndItShowsUpEvenWhenEmpty()
    {
        var user = await NewUserAsync();

        var response = await user.PostAsJsonAsync("/api/folders", new { name = "  Minha    pasta  " });
        var data = await DataAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Minha pasta", data.GetProperty("name").GetString()); // aparada e sem espaços repetidos
        Assert.Equal(0, data.GetProperty("noteCount").GetInt32());

        var listed = Find(await FoldersAsync(user), data.GetProperty("id").GetInt32());
        Assert.Equal("Minha pasta", listed.GetProperty("name").GetString());
        Assert.Equal(0, listed.GetProperty("noteCount").GetInt32());
    }

    [Fact]
    public async Task List_IsSortedByNameIgnoringCase()
    {
        var user = await NewUserAsync();
        await CreateFolderAsync(user, "beta");
        await CreateFolderAsync(user, "Alfa");
        await CreateFolderAsync(user, "Gama");

        var names = (await FoldersAsync(user)).EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();

        Assert.Equal(["Alfa", "beta", "Gama"], names);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithABlankName_Returns400(string name)
    {
        var user = await NewUserAsync();

        var response = await user.PostAsJsonAsync("/api/folders", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithANameAboveTheLimit_Returns400_AndTheLimitItselfIsAccepted()
    {
        var user = await NewUserAsync();

        var tooLong = await user.PostAsJsonAsync("/api/folders", new { name = new string('a', 101) });
        var atTheLimit = await user.PostAsJsonAsync("/api/folders", new { name = new string('a', 100) });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Created, atTheLimit.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnExistingNameInAnyCase_Returns409()
    {
        var user = await NewUserAsync();
        await CreateFolderAsync(user, "Trabalho");

        var response = await user.PostAsJsonAsync("/api/folders", new { name = "  trabalho " });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Já existe uma pasta com esse nome", await MessageAsync(response));
    }

    [Fact]
    public async Task TwoUsersCanHaveFoldersWithTheSameName()
    {
        var alice = await NewUserAsync();
        var bob = await NewUserAsync();
        await CreateFolderAsync(alice, "Ideias");

        var response = await bob.PostAsJsonAsync("/api/folders", new { name = "Ideias" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Rename_ChangesTheName_AndKeepsTheNotesInIt()
    {
        var user = await NewUserAsync();
        var folderId = await CreateFolderAsync(user, "Antigo");
        await CreateNoteAsync(user, new { title = "Nota", content = "", folderId });

        var renamed = await user.PutAsJsonAsync($"/api/folders/{folderId}", new { name = "Novo nome" });
        var data = await DataAsync(renamed);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Novo nome", data.GetProperty("name").GetString());
        Assert.Equal(1, data.GetProperty("noteCount").GetInt32());

        var notes = await DataAsync(await user.GetAsync($"/api/notes?folderId={folderId}"));
        Assert.Equal("Novo nome", notes[0].GetProperty("folder").GetString());
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherFolder_Returns409()
    {
        var user = await NewUserAsync();
        await CreateFolderAsync(user, "Trabalho");
        var other = await CreateFolderAsync(user, "Pessoal");

        var response = await user.PutAsJsonAsync($"/api/folders/{other}", new { name = "TRABALHO" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rename_OnlyTheCaseOfItsOwnName_IsAllowed()
    {
        var user = await NewUserAsync();
        var id = await CreateFolderAsync(user, "trabalho");

        var response = await user.PutAsJsonAsync($"/api/folders/{id}", new { name = "Trabalho" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Trabalho", (await DataAsync(response)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task UnknownFolder_Returns404WithAFolderMessage()
    {
        var user = await NewUserAsync();

        var get = await user.GetAsync("/api/folders/999999");
        var put = await user.PutAsJsonAsync("/api/folders/999999", new { name = "x" });
        var delete = await user.DeleteAsync("/api/folders/999999");

        Assert.All([get, put, delete], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal("Pasta não encontrada", await MessageAsync(get));
    }

    // ---------- Notas dentro da pasta

    [Fact]
    public async Task ANoteCreatedWithAFolderId_ShowsTheFolderAndCountsInIt()
    {
        var user = await NewUserAsync();
        var folderId = await CreateFolderAsync(user, "Trabalho");

        var note = await CreateNoteAsync(user, new { title = "Reunião", content = "Pauta", folderId });

        Assert.Equal(folderId, note.GetProperty("folderId").GetInt32());
        Assert.Equal("Trabalho", note.GetProperty("folder").GetString());
        Assert.Equal(1, Find(await FoldersAsync(user), folderId).GetProperty("noteCount").GetInt32());
    }

    [Fact]
    public async Task NotesCanBeListedByFolderId_AndAFolderWithNoNotesListsNothing()
    {
        var user = await NewUserAsync();
        var work = await CreateFolderAsync(user, "Trabalho");
        var empty = await CreateFolderAsync(user, "Vazia");
        await CreateNoteAsync(user, new { title = "Dentro", content = "", folderId = work });
        await CreateNoteAsync(user, new { title = "Fora", content = "" });

        var inWork = await DataAsync(await user.GetAsync($"/api/notes?folderId={work}"));
        var inEmpty = await DataAsync(await user.GetAsync($"/api/notes?folderId={empty}"));
        var unknown = await DataAsync(await user.GetAsync("/api/notes?folderId=999999"));

        Assert.Equal(["Dentro"], inWork.EnumerateArray().Select(n => n.GetProperty("title").GetString()).ToList());
        Assert.Equal(0, inEmpty.GetArrayLength());
        Assert.Equal(0, unknown.GetArrayLength());
    }

    [Fact]
    public async Task MovingANote_UpdatesBothFoldersCounts_AndNullTakesItOutOfTheFolder()
    {
        var user = await NewUserAsync();
        var from = await CreateFolderAsync(user, "Origem");
        var to = await CreateFolderAsync(user, "Destino");
        var note = await CreateNoteAsync(user, new { title = "Nota", content = "", folderId = from });
        var noteId = note.GetProperty("id").GetInt32();

        var moved = await user.PutAsJsonAsync($"/api/notes/{noteId}", new { title = "Nota", content = "", folderId = to });
        Assert.Equal(to, (await DataAsync(moved)).GetProperty("folderId").GetInt32());

        var folders = await FoldersAsync(user);
        Assert.Equal(0, Find(folders, from).GetProperty("noteCount").GetInt32());
        Assert.Equal(1, Find(folders, to).GetProperty("noteCount").GetInt32());

        var removed = await user.PutAsJsonAsync($"/api/notes/{noteId}", new { title = "Nota", content = "" });
        var data = await DataAsync(removed);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("folderId").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("folder").ValueKind);
        Assert.Equal(0, Find(await FoldersAsync(user), to).GetProperty("noteCount").GetInt32());
    }

    [Fact]
    public async Task UsingAnotherUsersFolder_Returns404_AndTheNoteIsNotCreated()
    {
        var alice = await NewUserAsync();
        var bob = await NewUserAsync();
        var aliceFolder = await CreateFolderAsync(alice, "Segredos");

        var response = await bob.PostAsJsonAsync("/api/notes", new { title = "Intruso", content = "", folderId = aliceFolder });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Pasta não encontrada", await MessageAsync(response));
        Assert.Equal(0, (await DataAsync(await bob.GetAsync("/api/notes"))).GetArrayLength());
        Assert.Equal(0, Find(await FoldersAsync(alice), aliceFolder).GetProperty("noteCount").GetInt32());
    }

    [Fact]
    public async Task FoldersAreInvisibleToOtherUsers()
    {
        var alice = await NewUserAsync();
        var bob = await NewUserAsync();
        var aliceFolder = await CreateFolderAsync(alice, "Só da Alice");

        Assert.Equal(0, (await FoldersAsync(bob)).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/folders/{aliceFolder}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/folders/{aliceFolder}", new { name = "Roubada" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/folders/{aliceFolder}")).StatusCode);

        // e continua intacta para a dona
        Assert.Equal("Só da Alice", Find(await FoldersAsync(alice), aliceFolder).GetProperty("name").GetString());
    }

    // ---------- Excluir pasta não apaga notas

    [Fact]
    public async Task DeletingAFolder_KeepsItsNotes_WithoutAFolder()
    {
        var user = await NewUserAsync();
        var folderId = await CreateFolderAsync(user, "Temporária");
        var note = await CreateNoteAsync(user, new { title = "Sobrevive", content = "Texto", folderId });
        var noteId = note.GetProperty("id").GetInt32();

        var deleted = await user.DeleteAsync($"/api/folders/{folderId}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/folders/{folderId}")).StatusCode);

        var kept = await DataAsync(await user.GetAsync($"/api/notes/{noteId}"));
        Assert.Equal("Sobrevive", kept.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, kept.GetProperty("folderId").ValueKind);
        Assert.Equal(JsonValueKind.Null, kept.GetProperty("folder").ValueKind);
    }

    // ---------- Clientes antigos: pasta só pelo nome

    [Fact]
    public async Task AnOldClientSendingOnlyTheFolderName_CreatesTheFolderOnce_AndReusesItIgnoringCase()
    {
        var user = await NewUserAsync();

        var first = await CreateNoteAsync(user, new { title = "Um", content = "", folder = "Trabalho" });
        var second = await CreateNoteAsync(user, new { title = "Dois", content = "", folder = "  trabalho " });

        Assert.Equal(first.GetProperty("folderId").GetInt32(), second.GetProperty("folderId").GetInt32());
        Assert.Equal("Trabalho", second.GetProperty("folder").GetString());

        var folders = await FoldersAsync(user);
        Assert.Equal(1, folders.GetArrayLength());
        Assert.Equal(2, folders[0].GetProperty("noteCount").GetInt32());
    }

    [Fact]
    public async Task WhenBothAreSent_TheFolderIdWins()
    {
        var user = await NewUserAsync();
        var byId = await CreateFolderAsync(user, "Escolhida");

        var note = await CreateNoteAsync(user, new { title = "Nota", content = "", folderId = byId, folder = "Outra" });

        Assert.Equal(byId, note.GetProperty("folderId").GetInt32());
        Assert.Equal("Escolhida", note.GetProperty("folder").GetString());
        Assert.Equal(1, (await FoldersAsync(user)).GetArrayLength()); // "Outra" não foi criada
    }

    [Fact]
    public async Task TheOldFolderFilterByNameStillWorks_AndIgnoresCase()
    {
        var user = await NewUserAsync();
        var work = await CreateFolderAsync(user, "Trabalho");
        await CreateNoteAsync(user, new { title = "Dentro", content = "", folderId = work });
        await CreateNoteAsync(user, new { title = "Fora", content = "" });

        var byName = await DataAsync(await user.GetAsync("/api/notes?folder=trabalho"));

        Assert.Equal(["Dentro"], byName.EnumerateArray().Select(n => n.GetProperty("title").GetString()).ToList());
    }

    [Fact]
    public async Task TheOldFoldersNameListIncludesEmptyFolders()
    {
        var user = await NewUserAsync();
        await CreateFolderAsync(user, "Vazia");

        var names = await DataAsync(await user.GetAsync("/api/notes/folders"));

        Assert.Equal(["Vazia"], names.EnumerateArray().Select(n => n.GetString()).ToList());
    }

    // ---------- Autenticação

    [Theory]
    [InlineData("GET", "/api/folders")]
    [InlineData("GET", "/api/folders/1")]
    [InlineData("POST", "/api/folders")]
    [InlineData("PUT", "/api/folders/1")]
    [InlineData("DELETE", "/api/folders/1")]
    public async Task EveryFolderEndpointRequiresLogin(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { name = "x" });

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
