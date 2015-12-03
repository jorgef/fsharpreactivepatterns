namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("fsharpreactivepatterns")>]
[<assembly: AssemblyProductAttribute("fsharpreactivepatterns")>]
[<assembly: AssemblyDescriptionAttribute("F# reactive patterns")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
