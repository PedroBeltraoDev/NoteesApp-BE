using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.Options;

// Contas de demonstração: temporárias, criadas com um clique na tela de login
public class DemoOptions
{
    public const string SectionName = "Demo";

    // Quanto tempo a conta demo dura. Passado esse prazo o token deixa de valer, não dá para renovar e os dados são apagados
    [Range(1, 240)]
    public int Minutes { get; set; } = 30;

    // Teto de demos ao mesmo tempo (cada uma grava notas no banco): evita que alguém encha o banco criando demos sem parar
    [Range(1, 10_000)]
    public int MaxActive { get; set; } = 100;
}
