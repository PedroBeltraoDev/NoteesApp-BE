using NotesApp.Api.Models;

namespace NotesApp.Api.Services;

// O que uma conta demo já traz, para a pessoa ver pastas, tags e favoritas funcionando sem precisar criar nada
public static class DemoSeed
{
    public static (List<Folder> Folders, List<Note> Notes) Build(User user, int minutes, DateTime now)
    {
        Note NewNote(string title, string content, int minutesAgo, string[] tags, bool pinned = false) => new()
        {
            UserId = user.Id,
            Title = title,
            Content = content,
            Tags = [.. tags],
            IsPinned = pinned,
            CreatedAt = now.AddMinutes(-minutesAgo),
            UpdatedAt = now.AddMinutes(-minutesAgo)
        };

        var folder = new Folder
        {
            UserId = user.Id,
            Name = "Exemplos",
            CreatedAt = now.AddMinutes(-3),
            Notes =
            [
                NewNote(
                    "Bem-vindo à conta demo",
                    $"Esta conta é temporária: ela e tudo o que você criar aqui serão apagados em {minutes} minutos. Experimente criar, editar e organizar notas à vontade.",
                    1, ["demo"], pinned: true),
                NewNote(
                    "Pastas e tags",
                    "Uma pasta agrupa notas (cada nota fica em uma só). Tags são rótulos livres: uma nota pode ter várias. Use as abas Pastas e Tags para navegar.",
                    2, ["dica", "tags"])
            ]
        };

        var loose = NewNote(
            "Favoritas e ordem",
            "Toque na estrela para favoritar e use o botão Favoritas para ver só elas. A ordem das notas pode ser por data de criação ou de edição.",
            3, ["dica"]);

        return ([folder], [loose]);
    }
}
