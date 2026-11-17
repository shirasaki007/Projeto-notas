namespace Notas.Tests;

public class NoteServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task List_returns_only_the_current_users_notes()
    {
        await _env.NewNote("da Ana");
        _env.User.Id = "user-b";
        await _env.NewNote("do Bruno");

        var listB = await _env.Notes.ListAsync(new NoteQuery());
        Assert.Single(listB.Items);
        Assert.Equal("do Bruno", listB.Items[0].Title);

        _env.User.Id = "user-a";
        var listA = await _env.Notes.ListAsync(new NoteQuery());
        Assert.Single(listA.Items);
        Assert.Equal("da Ana", listA.Items[0].Title);
    }

    [Fact]
    public async Task Another_user_cannot_read_update_or_delete_a_note()
    {
        var note = await _env.NewNote("segredo", "conteúdo privado");
        _env.User.Id = "user-b";

        Assert.Equal(ErrorKind.NotFound, (await _env.Notes.GetAsync(note.Id)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await _env.Notes.UpdateAsync(note.Id,
            new NoteSaveRequest { Title = "hack", Revision = note.Revision })).Error!.Kind);
        Assert.False((await _env.Notes.ApplyAsync(note.Id, NoteAction.Trash)).Succeeded);
        Assert.Empty(await _env.Notes.GetVersionsAsync(note.Id));

        _env.User.Id = "user-a";
        var still = await _env.Notes.GetAsync(note.Id);
        Assert.True(still.Succeeded);
        Assert.Equal("segredo", still.Value!.Title);
        Assert.False(still.Value.IsTrashed);
    }

    [Fact]
    public async Task Cannot_assign_a_folder_that_belongs_to_another_user()
    {
        _env.User.Id = "user-b";
        var folder = (await _env.Folders.CreateAsync("Privada do Bruno")).Value!;

        _env.User.Id = "user-a";
        var r = await _env.Notes.CreateAsync(new NoteSaveRequest { Title = "x", FolderId = folder.Id });
        Assert.Equal(ErrorKind.Validation, r.Error!.Kind);
    }

    [Fact]
    public async Task Update_with_stale_revision_returns_conflict()
    {
        var note = await _env.NewNote("v1", "a");

        var first = await _env.Notes.UpdateAsync(note.Id, new NoteSaveRequest { Title = "v2", Content = "b", Revision = note.Revision });
        Assert.True(first.Succeeded);
        Assert.Equal(note.Revision + 1, first.Value!.Revision);

        var stale = await _env.Notes.UpdateAsync(note.Id, new NoteSaveRequest { Title = "v3", Content = "c", Revision = note.Revision });
        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
    }

    [Fact]
    public async Task Tags_are_normalized_and_deduplicated()
    {
        var note = await _env.NewNote("t", "", "  Trabalho ", "TRABALHO", "#Ideias");
        Assert.Equal(new[] { "ideias", "trabalho" }, note.Tags.ToArray());
    }

    [Fact]
    public async Task Tag_list_counts_only_non_trashed_notes_and_omits_empty_tags()
    {
        await _env.NewNote("active", "", "common");
        var trashed = await _env.NewNote("trashed", "", "common", "only-in-trash");
        await _env.Notes.ApplyAsync(trashed.Id, NoteAction.Trash);

        var tags = await _env.Tags.ListAsync();

        var tag = Assert.Single(tags);
        Assert.Equal("common", tag.Name);
        Assert.Equal(1, tag.NoteCount);
    }

    [Fact]
    public async Task Too_many_or_too_long_tags_are_rejected()
    {
        var many = await _env.Notes.CreateAsync(new NoteSaveRequest { Tags = [.. Enumerable.Range(0, 11).Select(i => $"t{i}")] });
        Assert.Equal(ErrorKind.Validation, many.Error!.Kind);

        var longTag = await _env.Notes.CreateAsync(new NoteSaveRequest { Tags = [new string('x', 31)] });
        Assert.Equal(ErrorKind.Validation, longTag.Error!.Kind);
    }

    [Fact]
    public async Task Trash_and_restore_move_note_between_views()
    {
        var note = await _env.NewNote("lixo");

        Assert.True((await _env.Notes.ApplyAsync(note.Id, NoteAction.Trash)).Succeeded);
        Assert.Empty((await _env.Notes.ListAsync(new NoteQuery())).Items);
        Assert.Single((await _env.Notes.ListAsync(new NoteQuery(View: NoteView.Trash))).Items);
        Assert.Equal(1, (await _env.Notes.GetCountsAsync()).Trash);

        // nota na lixeira não pode ser editada
        var edit = await _env.Notes.UpdateAsync(note.Id, new NoteSaveRequest { Title = "x", Revision = note.Revision });
        Assert.Equal(ErrorKind.Conflict, edit.Error!.Kind);

        Assert.True((await _env.Notes.ApplyAsync(note.Id, NoteAction.Restore)).Succeeded);
        Assert.Single((await _env.Notes.ListAsync(new NoteQuery())).Items);
    }

    [Fact]
    public async Task Pin_favorite_and_archive_drive_the_views_and_counts()
    {
        var a = await _env.NewNote("a");
        var b = await _env.NewNote("b");
        await _env.Notes.ApplyAsync(a.Id, NoteAction.Pin);
        await _env.Notes.ApplyAsync(a.Id, NoteAction.Favorite);
        await _env.Notes.ApplyAsync(b.Id, NoteAction.Archive);

        var counts = await _env.Notes.GetCountsAsync();
        Assert.Equal(new NoteCounts(All: 1, Pinned: 1, Favorites: 1, Archived: 1, Trash: 0), counts);

        Assert.Equal("a", (await _env.Notes.ListAsync(new NoteQuery(View: NoteView.Pinned))).Items.Single().Title);
        Assert.Equal("b", (await _env.Notes.ListAsync(new NoteQuery(View: NoteView.Archived))).Items.Single().Title);

        // arquivar uma nota fixada remove o pin
        await _env.Notes.ApplyAsync(a.Id, NoteAction.Archive);
        Assert.False((await _env.Notes.GetAsync(a.Id)).Value!.IsPinned);
    }

    [Fact]
    public async Task Flag_changes_do_not_break_the_editors_revision()
    {
        var note = await _env.NewNote("x", "y");
        await _env.Notes.ApplyAsync(note.Id, NoteAction.Pin);

        var save = await _env.Notes.UpdateAsync(note.Id, new NoteSaveRequest { Title = "x2", Content = "y", Revision = note.Revision });
        Assert.True(save.Succeeded);
    }

    [Fact]
    public async Task Search_matches_title_content_and_tags_case_insensitively()
    {
        await _env.NewNote("Reunião de planejamento", "pauta do trimestre");
        await _env.NewNote("Compras", "leite e pão", "mercado");

        Assert.Single((await _env.Notes.ListAsync(new NoteQuery(Search: "PLANEJAMENTO"))).Items);
        Assert.Single((await _env.Notes.ListAsync(new NoteQuery(Search: "trimestre"))).Items);
        Assert.Single((await _env.Notes.ListAsync(new NoteQuery(Search: "mercado"))).Items);
        Assert.Empty((await _env.Notes.ListAsync(new NoteQuery(Search: "inexistente"))).Items);
    }

    [Fact]
    public async Task Filtering_by_tag_and_folder_works()
    {
        var folder = (await _env.Folders.CreateAsync("Trabalho")).Value!;
        var created = await _env.Notes.CreateAsync(new NoteSaveRequest { Title = "n1", FolderId = folder.Id, Tags = ["x"] });
        await _env.NewNote("n2", "", "y");

        var tagId = (await _env.CreateContext().Tags.FirstAsync(t => t.Name == "x")).Id;
        Assert.Equal("n1", (await _env.Notes.ListAsync(new NoteQuery(TagId: tagId))).Items.Single().Title);
        Assert.Equal("n1", (await _env.Notes.ListAsync(new NoteQuery(FolderId: folder.Id))).Items.Single().Title);
        Assert.Equal("Trabalho", created.Value!.FolderName);
    }

    [Fact]
    public async Task Editing_creates_a_version_and_restoring_it_brings_the_text_back()
    {
        var note = await _env.NewNote("original", "texto 1");
        var upd = await _env.Notes.UpdateAsync(note.Id, new NoteSaveRequest { Title = "editado", Content = "texto 2", Revision = note.Revision });

        var versions = await _env.Notes.GetVersionsAsync(note.Id);
        Assert.Single(versions);
        Assert.Equal("original", versions[0].Title);

        var restored = await _env.Notes.RestoreVersionAsync(note.Id, versions[0].Id);
        Assert.True(restored.Succeeded);
        Assert.Equal("original", restored.Value!.Title);
        Assert.Equal("texto 1", restored.Value.Content);
        Assert.True(restored.Value.Revision > upd.Value!.Revision);
        Assert.Equal(2, (await _env.Notes.GetVersionsAsync(note.Id)).Count); // restauração é reversível
    }

    [Fact]
    public async Task Permanent_delete_only_works_for_trashed_notes()
    {
        var note = await _env.NewNote("x");
        Assert.False((await _env.Notes.DeletePermanentlyAsync(note.Id)).Succeeded);

        await _env.Notes.ApplyAsync(note.Id, NoteAction.Trash);
        Assert.True((await _env.Notes.DeletePermanentlyAsync(note.Id)).Succeeded);
        Assert.False((await _env.Notes.GetAsync(note.Id)).Succeeded);
    }

    [Fact]
    public async Task Overlong_title_or_content_is_rejected_by_the_backend()
    {
        var r = await _env.Notes.CreateAsync(new NoteSaveRequest { Title = new string('a', 201) });
        Assert.Equal(ErrorKind.Validation, r.Error!.Kind);
    }

    [Fact]
    public async Task Deleting_a_folder_keeps_its_notes_unfiled()
    {
        var folder = (await _env.Folders.CreateAsync("Temp")).Value!;
        var note = (await _env.Notes.CreateAsync(new NoteSaveRequest { Title = "n", FolderId = folder.Id })).Value!;

        Assert.True((await _env.Folders.DeleteAsync(folder.Id)).Succeeded);

        var after = (await _env.Notes.GetAsync(note.Id)).Value!;
        Assert.Null(after.FolderId);
    }

    [Fact]
    public async Task Folder_names_are_unique_per_user_case_insensitively()
    {
        Assert.True((await _env.Folders.CreateAsync("Projetos")).Succeeded);
        Assert.Equal(ErrorKind.Conflict, (await _env.Folders.CreateAsync("projetos")).Error!.Kind);

        _env.User.Id = "user-b";
        Assert.True((await _env.Folders.CreateAsync("Projetos")).Succeeded); // outro usuário pode usar o mesmo nome
    }
}

public class SnippetAndTagRuleTests
{
    [Theory]
    [InlineData("  #Trabalho ", "trabalho")]
    [InlineData("Duas   Palavras", "duas palavras")]
    public void Tag_normalization(string raw, string expected)
    {
        Assert.True(TagRules.TryNormalize(raw, out var n));
        Assert.Equal(expected, n);
    }

    [Fact]
    public void Blank_tag_is_rejected() => Assert.False(TagRules.TryNormalize("  # ", out _));

    [Fact]
    public void Snippet_strips_markdown_noise_and_truncates()
    {
        Assert.Equal("Título texto", Snippets.From("# Título\n\n**texto**"));
        Assert.EndsWith("…", Snippets.From(new string('a', 300), 50));
    }
}
