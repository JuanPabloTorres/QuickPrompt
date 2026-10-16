using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;

public sealed class EverydaySearchTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"quickprompt-daily-search-"+Guid.NewGuid());
    private LocalStore Store()=>new(Path.Combine(folder,"test.sqlite"));

    [Theory]
    [InlineData("mensaje reunion",true)]
    [InlineData("REUNIÓN MENSAJE",true)]
    [InlineData(" \t reunión \n mensaje ",true)]
    [InlineData("equipo persona",true)]
    [InlineData("mensaje inexistente",false)]
    [InlineData("",true)]
    [InlineData("   ",true)]
    [InlineData("reunión reunión",true)]
    [InlineData("%",false)]
    [InlineData("mensaje' OR 1=1 --",false)]
    public void RememberedTermsMatchAcrossFieldsWithoutRequiringExactOrder(string query,bool expected)
    {
        var source=new Template{Title="Mensaje para coordinar una reunión",Description="Equipo de trabajo",Template="Escribe a <persona>."};
        Assert.Equal(expected,PromptSearch.Matches(source,query));
        Assert.Equal("Mensaje para coordinar una reunión",source.Title);
    }

    [Fact] public void CatalogRequiresEveryTermAndTreatsSqlCharactersLiterallyWithoutWriting()
    {
        var s=Store();var match=s.Save(new(){Title="Mensaje para coordinar una reunión",Description="Equipo de trabajo",Template="Escribe a <persona>."});
        var other=s.Save(new(){Title="Lectura al 10%",Template="Lee un capítulo."});var before=s.Export();
        Assert.Equal(match.Id,Assert.Single(s.QueryTemplates(new(Search:"  REUNION\t mensaje  ")).Items).Id);
        Assert.Equal(match.Id,Assert.Single(s.QueryTemplates(new(Search:"persona equipo")).Items).Id);
        Assert.Empty(s.QueryTemplates(new(Search:"mensaje inexistente")).Items);
        Assert.Empty(s.QueryTemplates(new(Search:"mensaje' OR 1=1 --")).Items);
        Assert.Equal(other.Id,Assert.Single(s.QueryTemplates(new(Search:"%")).Items).Id);
        Assert.Equal(2,s.QueryTemplates(new(Search:" \n ")).Total);
        Assert.Equal(before,s.Export());
    }

    [Fact] public void TermSearchPreservesCountsPagingFavoritesGroupsAndArchiveFilters()
    {
        var s=Store();var a=s.Save(new(){Title="Mensaje de reunión A",Template="Texto",IsFavorite=true});var b=s.Save(new(){Title="Mensaje de reunión B",Template="Texto",IsFavorite=true});
        s.Save(new(){Title="Mensaje de reunión C",Template="Texto"});s.Save(new(){Title="Mensaje de reunión D",Template="Texto",IsFavorite=true,IsActive=false});
        var group=s.SaveGroup(new(){Name="Trabajo"});s.AssignGroup(group.Id,"template",[a.Id,b.Id]);
        var first=s.QueryTemplates(new(Search:"reunion mensaje",Favorites:true,GroupId:group.Id,Sort:"title",Size:1));
        var second=s.QueryTemplates(new(Search:"reunion mensaje",Favorites:true,GroupId:group.Id,Sort:"title",Size:1,Page:1));
        Assert.Equal(2,first.Total);Assert.Equal(a.Id,Assert.Single(first.Items).Id);Assert.Equal(2,second.Total);Assert.Equal(b.Id,Assert.Single(second.Items).Id);
        Assert.Equal(1,s.QueryTemplates(new(Search:"mensaje reunion",Active:false)).Total);
        Assert.Empty(s.QueryTemplates(new(Search:"mensaje reunion",Favorites:true,Ungrouped:true)).Items);
    }

    public void Dispose(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
}
