using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy(name: MyAllowSpecificOrigins,
//                      policy =>
//                      {
//                          policy.WithOrigins("http://example.com",
//                                              "http://www.contoso.com");
//                      });
//});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
        .AddAuthentication(o =>
        {
            // This forces challenge results to be handled by Google OpenID Handler, so there's no
            // need to add an AccountController that emits challenges for Login.
            o.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
            // This forces forbid results to be handled by Google OpenID Handler, which checks if
            // extra scopes are required and does automatic incremental auth.
            o.DefaultForbidScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
            // Default scheme that will handle everything else.
            // Once a user is authenticated, the OAuth2 token info is stored in cookies.
            o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddGoogleOpenIdConnect(options =>
        {
            options.ClientId = Environment.GetEnvironmentVariable("WebClient2_ClientId");
            options.ClientSecret = Environment.GetEnvironmentVariable("WebClient2_ClientSecret");
        });

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".AdventureWorks.Session";
    options.IdleTimeout = TimeSpan.FromSeconds(200);
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
//app.UseRouting();

//app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllers();

//app.Run(async (context) => { Session });

app.Run();
