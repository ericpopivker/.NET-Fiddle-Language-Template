.NET Fiddle - Language Template
===============================

Uses this template to extend .NET Fiddle with support for a .NET Languages like Nemerle and .NET Web frameworks like NancyFX into .NET Fiddle.

Template includes basic .NET Fiddle infrastructure and sample implementation of "CSharp Console".


The preliminary process will be:

1. Fork this repo to something like ".NET Fiddle <Name of the language>".  Like ".NET Fiddle Nemerle"
2. Add new projects specific to the language/.NET Web framework.  See Solution Structure section below.
3. When you finished implementing CodeHelper, Automated Tests and Web (CodeMirror), send an email to dotnetfiddle@entechsolutioins.com and someone will pull your code and test it.  We will communicate any issues through GitHub.  
4. After all the issues have been resolved we will let you know when your language/web framework will be rolled.



# Solution Structure


- 

For ex. DotNetFiddle.NemerleScript, DotNetFiddle.NemerleScript.Tests, DotNetFiddle.NemerleScript.Web.  Names like NemerleScript - consists of language and project type.  Some languages like C#, VB.NET may support Console/Script/MVC, while others only Script - like F#, Nemerle.




# Nemerle Notes

NemerleScript can be implemented similarly to CSharpConsole example
There shouldn't be any changes to infrastructure, but it may require some extra sandbox permissions or some extra steps during azure rollout.


# NancyFX Notes












