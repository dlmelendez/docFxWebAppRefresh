# ElCamino.DocFx.WebAppRefresh
[DocFx](https://dotnet.github.io/docfx/index.html) build middleware that allows you to setup a .net core web app project and adds a file watcher to your content files. When you change a content file, such as markdown, css, etc, the middleware automatically starts a docFx build in the background regenerating the target html files.
[Sample .net core web application](https://github.com/dlmelendez/docFxWebAppRefresh/tree/master/sample/ElCamino.DocFx.WebAppRefresh.Sample) 

**This library should be configured for local development only as shown below!**
1. Create a new .net core web application 'Empty' project
```
dotnet new web
```
or 

Use Visual Studio new project, web application, Empty.

2. Use the nuget package manager to install **docfx.console**
```
dotnet add package docfx.console 
``` 
3. **Build** the project (this will generate all of the docfx files)
4. Rename the **_site** folder to **wwwroot**.
5. Edit the *.gitignore* file, remove **_site**, add **wwwroot** and **log.txt**
```
wwwroot
log.txt
```
6. Edit the *docfx.json*, rename **_site** and replace with **wwwroot**
```json
{
    "dest": "wwwroot"
}
```
7. Use the nuget package manager to install **ElCamino.DocFx.WebAppRefresh**
```
dotnet add package ElCamino.DocFx.WebAppRefresh 
```
8. Edit the Startup.cs
```c#
public class Startup
{
    ...
    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            // only use in development, not for production
            app.UseDocFxBuildRefresh(env.ContentRootPath, env.WebRootPath);
        }
        app.UseDefaultFiles();

        app.UseStaticFiles();
    }
...
```
9. Debug the web application. Change a content file (markdown, css, etc) and watch the debug output for the docFx build output and refresh the page to see your changes.
