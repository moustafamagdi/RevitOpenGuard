using System;
using Autodesk.Revit.DB;
using RevitOpenGuard.Policies;
using RevitOpenGuard.Infrastructure;

class Program
{
    static int count;
    static void Assert(bool value, string name) { if (!value) throw new Exception(name); count++; }
    static void Main()
    {
        var id = new Guid("b44c8ba0-7a86-44c1-bbf1-2de8e2017266");
        var failure = new FailureMessageAccessor(id, "One or more dimension references are or have become invalid.");
        CatalogStore.Current.Rules.Add(new CatalogRule { Id="Text.InvalidDimensionReferences.DeleteDimensions", Kind="Failure", Action="DeleteElements", MinRevit=2022, MaxRevit=2027, AllText=new[]{"one or more dimension references are or have become invalid"} });
        var policy = FailurePolicies.Match(failure)!;
        Assert(policy.FallbackResolution==FailureResolutionType.DetachElements,"Observed 2024 failure gets fallback");
        int detachChecks=0;
        Assert(FailurePolicies.SelectResolution(policy, r=>{detachChecks++;return true;},r=>true)==FailureResolutionType.DeleteElements,"Prefer permitted deletion");
        Assert(detachChecks==0,"Do not inspect fallback when deletion is permitted");
        Assert(FailurePolicies.SelectResolution(policy,r=>true,r=>r==FailureResolutionType.DetachElements)==FailureResolutionType.DetachElements,"Use removal when delete forbidden");
        Assert(FailurePolicies.SelectResolution(policy,r=>false,r=>r==FailureResolutionType.DetachElements)==null,"Removal must be offered");
        Assert(FailurePolicies.SelectResolution(policy,r=>true,r=>false)==null,"Both resolutions forbidden leaves normal UI");
        Assert(FailurePolicies.Match(new FailureMessageAccessor(Guid.NewGuid(),failure.Message))!.FallbackResolution==null,"Same text with different ID has no fallback");
        CatalogStore.RevitYear=2025;
        Assert(FailurePolicies.Match(failure)!.FallbackResolution==null,"No unobserved-year fallback");
        CatalogStore.RevitYear=2024;
        CatalogStore.Current.Rules[0].Action="DetachElements";
        policy=FailurePolicies.Match(failure)!;
        Assert(policy.FallbackResolution==null,"Do not change catalog removal into deletion");
        Assert(FailurePolicies.SelectResolution(policy,r=>false,r=>true)==null,"Primary removal must also be offered");
        Assert(FailurePolicies.SelectResolution(policy,r=>true,r=>true)==FailureResolutionType.DetachElements,"Existing removal action unchanged");
        Assert(FailurePolicies.Match(new FailureMessageAccessor(id,"Unrelated failure"))==null,"Unknown failures remain unmatched");
        Console.WriteLine("PASS: "+count+" recovery policy and resolution tests. Production policy code with API stubs; no live Revit execution.");
    }
}

namespace RevitOpenGuard.Infrastructure
{
    static class CatalogStore { internal static Catalog Current = new Catalog(); internal static int RevitYear=2024; }
}
namespace Autodesk.Revit.DB
{
    enum FailureResolutionType { DeleteElements, DetachElements }
    class FailureDefinitionId { internal Guid Guid; internal FailureDefinitionId(Guid id){Guid=id;} }
    class FailureMessageAccessor
    {
        readonly Guid id; internal string Message;
        internal FailureMessageAccessor(Guid id,string message){this.id=id;Message=message;}
        internal FailureDefinitionId GetFailureDefinitionId()=>new FailureDefinitionId(id);
        internal string GetDescriptionText()=>Message;
    }
    static class BuiltInFailures
    {
        internal static class DimensionFailures
        {
            internal static FailureDefinitionId LinearConstraintNotParallel=new FailureDefinitionId(Guid.NewGuid());
            internal static FailureDefinitionId RadialDimensionCannotProjectToArc=new FailureDefinitionId(Guid.NewGuid());
            internal static FailureDefinitionId DimensionPerpendicularToView=new FailureDefinitionId(Guid.NewGuid());
        }
    }
}
