.NET Fiddle - Language Template
===============================

Use this template to extend .NET Fiddle with support for .NET Languages like Nemerle and .NET Web frameworks like NancyFX into .NET Fiddle.

Template includes basic .NET Fiddle infrastructure and sample implementation of "CSharp Console".


The preliminary process is:

1. Fork this repo to something like ".NET Fiddle <Name of the language>".  Like ".NET Fiddle Nemerle"
2. Add new projects specific to the language/.NET Web framework.  See Solution Structure section below.
3. When you finished implementing Custom CodeHelper, Automated Tests and Web (CodeMirror), send an email to dotnetfiddle at entechsolutions dot com and someone will pull your code and test it.  We will communicate any issues through GitHub.
4. After all the issues have been resolved we will let you know when your language/web framework will be rolled.
5. After rollout keep us posted when there is a big update to language/web framework by updating your fork and sending us an email 


We will keep improving this process every time we add new language / web framework.


## Solution Structure

Infrastructure

- DotNetFiddle.Infrastructure - all bases classes and helpers
- DotNetFiddle.RunContainer - assembly used to execute fiddles in controlled sanbax - AppDomain
- DotNetFiddle.RunContainer.Helpers - implements Dump() method


CSharp Console implementation

- DotNetFiddle.CSharpConsole   - has CSharpConsoleCodeHelper which implements compiliaton, intellisence, validation and execution of CSharp Console fiddles
- DotNetFiddle.CSharpConsole.Tests - unit tests for CSharpConsoleCodeHelper being called directly as well as hosted within RunContainer
- DotNetFiddle.CSharpConsole.Web - CodeMirror template supporting CSharp 


For .NET Language like Nemerle you would add

- DotNetFiddle.NemerleScript - NemerleScriptCodeHelper.cs and supporting classes
- DotNetFiddle.NemerleScript.Tests - all tests like CSharpConsole
- DotNetFiddle.NemerleScript.Web - CodeMirror template that supports Nemerle

Name like NemerleScript - consists of language and project type.  Some languages like C#, VB.NET may support Console/Script/MVC, while others only Script - like F#, Nemerle.

For Web Framwork like NancyFX you would add:

- DotNetFiddle.NancyFx - NancyFxCodeHelper.cs and supporting classes.  Can be using CSharpCodeHelper internally.
- DotNetFiddle.NancyFx.Tests - automated tests. Probably a little different then for CSharp Console
- DotNetFiddle.NancyFx.Web - probably not needed as long as NancyFx uses C# and Razor



## Nemerle Notes

NemerleScript can be implemented similarly to CSharpConsole example
There shouldn't be any changes to infrastructure, but it may require some extra sandbox permissions or some extra steps during azure rollout.  



## NancyFX Notes

For NancyFX the data structures will need to be extended to work a bit more like MVC.

Here are the data structures that will need to be extended:


NancyFxCodeBlock : CodeBlock   (See MvcCodeBlock)
 View
 Controller
 NancyModule
 

NancyFxValidationError : ValidationError
 NancyFxFileType  (similar to MvcFileType)


NancyFcCodeHelper.GetAutoCompleteItems() need to take one extra parameter
  NancyFxFileType
  
NOTE: we don't support Auto Complete in MVC view right now, so may not be possibly in NancyFx






