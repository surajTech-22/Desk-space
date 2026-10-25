using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskSpace;

// Read-only eligibility gate, evaluated against EVERY physical destination
// cell BEFORE any Excel COM format write. No value is copied by this class.
internal static class DeskSpaceExcelFullRangeGateV107
{
    internal enum Status { EntirelyEmpty, CompleteMatchingLayout, RejectPartial, RejectMismatch }
    internal sealed record Cell(bool HasData, string StoredType);
    internal sealed record Decision(Status State,int Total,int Filled,
        int RequiredFilled,string Explanation)
    {
        public bool MayApply => State is Status.EntirelyEmpty or Status.CompleteMatchingLayout;
    }
    // Captured source and live destination are both indexed in row-major
    // physical-cell order, INCLUDING blank/merged-underlying cells.
    internal static Decision Evaluate(IReadOnlyList<Cell> source,
        IReadOnlyList<Cell> destination)
    {
        if(source.Count==0 || source.Count!=destination.Count)
            return new(Status.RejectMismatch,destination.Count,0,0,
                "Selection shape does not match the recorded template.");
        int count=destination.Count;
        int filled=destination.Count(x=>x.HasData);
        int required=source.Count(x=>x.HasData);
        if(filled==0)
            return new(Status.EntirelyEmpty,count,0,required,
                "All destination cells are blank; formatting is eligible.");
        if(filled!=required)
            return new(Status.RejectPartial,count,filled,required,
                "Destination has "+filled+"/"+count+" occupied cells; template requires "+
                required+" at its saved positions. No changes allowed.");
        for(int index=0;index<count;index++)
        {
            bool shouldHaveData=source[index].HasData;
            bool hasData=destination[index].HasData;
            if(shouldHaveData!=hasData)
                return new(Status.RejectPartial,count,filled,required,
                    "Data exists in different cells than the captured template. "+
                    "No format is applied.");
            if(!shouldHaveData)continue;
            string wanted=Normalize(source[index].StoredType);
            string actual=Normalize(destination[index].StoredType);
            if(wanted!=actual)
                return new(Status.RejectMismatch,count,filled,required,
                    "Cell "+(index+1)+" contains "+actual+"; template expects "+
                    wanted+". No changes allowed.");
        }
        return new(Status.CompleteMatchingLayout,count,filled,required,
            "All populated destination cells match the captured occupancy "+
            "and input-type layout. Formatting is eligible.");
    }
    private static string Normalize(string value)
    {
        if(value.Equals("Date",StringComparison.OrdinalIgnoreCase))
            return "Number";
        return value.ToUpperInvariant() switch {
            "FORMULA"=>"Formula","NUMBER"=>"Number",
            "BOOLEAN"=>"Boolean","TEXT"=>"Text",
            "ERROR"=>"Error","BLANK"=>"Blank",_=>value
        };
    }
}
