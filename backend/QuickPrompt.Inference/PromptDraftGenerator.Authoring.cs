using System.Globalization;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed partial class PromptDraftGenerator
{
    private record AuthoringUnit(int Start,int End,string Text);
    private record QuotedBody(int Id,int Start,int End,string Text,int CharacterStart);
    private static QuotedBody[] QuotedBodies(string request,AuthoringUnit[] units)
    {
        var results=new List<QuotedBody>(); int? opening=null; string? closing=null;
        for(var index=0;index<units.Length;index++) {
            var token=units[index].Text;
            if(opening is { } start) {
                if(token!=closing)continue;
                var text=request[units[start].End..units[index].Start];
                if(!string.IsNullOrWhiteSpace(text))results.Add(new(results.Count,start+1,index-1,text,units[start].End));
                opening=null;closing=null;
            } else if(token is "«" or "“" or "\"" or "`") {
                opening=index;closing=token switch {"«"=>"»","“"=>"”",_=>token};
            }
        }
        return results.ToArray();
    }

    // Lexical references only: the model, not a keyword rule, assigns semantic roles.
    // Graphemes keep Unicode names and emoji intact; offsets always address the original.
    private static AuthoringUnit[] AuthoringUnits(string request)
    {
        var units=new List<AuthoringUnit>(); var elements=StringInfo.GetTextElementEnumerator(request);
        var previousWord=false;
        while(elements.MoveNext()) {
            var text=elements.GetTextElement(); var start=elements.ElementIndex; var end=start+text.Length;
            if(string.IsNullOrWhiteSpace(text)) { previousWord=false; continue; }
            var word=char.IsLetterOrDigit(text,0)||text=="_";
            if(word&&previousWord&&units[^1].End==start) {
                var previous=units[^1]; units[^1]=new(previous.Start,end,request[previous.Start..end]);
            } else units.Add(new(start,end,text));
            previousWord=word;
        }
        return units.ToArray();
    }

}
