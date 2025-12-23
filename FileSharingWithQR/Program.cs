using FileSharingWithQR.Services;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var allowedFrontendOrigins = builder.Configuration.GetSection("AllowedFrontendOrigins")
                                    .Get<string[]>() ?? new string[0];
Console.WriteLine("allowedFrontendOrigins.Count:" + allowedFrontendOrigins.Count());
Console.WriteLine(allowedFrontendOrigins[0]);

builder.Services.AddHostedService<FileCleanupService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("https://localhost:44309", "https://localhost:44310", "https://100.103.166.72", "https://qr-code-project-react.onrender.com") // React uygulamanýzýn adresi
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // <-- ÇOK ÖNEMLÝ
                      });
});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
        o.DefaultForbidScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        // Cross-site cookie gönderebilmek için
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    })
    .AddGoogleOpenIdConnect(options =>
    {
        options.ClientId = Environment.GetEnvironmentVariable("WebClient2_ClientId");
        options.ClientSecret = Environment.GetEnvironmentVariable("WebClient2_ClientSecret");

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                // 1. Frontend'in gönderdiði 'returnUrl' parametresini oku
                string returnUrl = context.Request.Query["returnUrl"];
                Console.WriteLine("returnUrl:" + returnUrl);

                // 2. === GÜVENLÝK KONTROLÜ ===
                // Gelen URL'in sizin izin verdiðiniz bir frontend adresi olduðundan emin olun.
                // Asla tanýmadýðýnýz bir URL'e yönlendirme yapmayýn!
                if (!string.IsNullOrEmpty(returnUrl) &&
                    (allowedFrontendOrigins.Contains(returnUrl) ||
                     (builder.Environment.IsDevelopment() && returnUrl.StartsWith("https://localhost"))))
                {
                    // 3. URL güvenliyse, Google'dan dönünce kullanmak üzere state'e kaydet
                    context.Properties.Items["custom_redirect_uri"] = returnUrl;
                }

                // 4. 'fetch' isteði kontrolümüz (bu ayný kalýr)
                if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.HandleResponse();
                }

                return Task.CompletedTask;
            },

            OnTicketReceived = context =>
            {
                // 1. Google'dan döndükten sonra state'e kaydettiðimiz URL'i geri al
                string redirectUrl;
                context.Properties.Items.TryGetValue("custom_redirect_uri", out redirectUrl);
                Console.WriteLine("redirectUrl:" + redirectUrl);

                
                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    //redirectUrl = allowedFrontendOrigins.FirstOrDefault() ?? "/";
                    context.ReturnUri = redirectUrl;
                }
                Console.WriteLine("redirectUrl:" + redirectUrl);

                // === DEÐÝÞÝKLÝK BURADA ===

                // 3. Yönlendirmeyi doðrudan Response'a yazmak YERÝNE,
                //    OpenID Connect handler'ýna akýþ bittiðinde nereye gitmesi gerektiðini söylüyoruz.
                //context.ReturnUri = redirectUrl;


                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".AdventureWorks.Session";
    options.IdleTimeout = TimeSpan.FromSeconds(200);
    options.Cookie.IsEssential = true;

    // Oturum cookie'si de cross-site olmalý
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 1. HTTPS yönlendirmesi (genellikle en baþta kalýr)
app.UseHttpsRedirection();

// 2. Yönlendirmeyi (Routing) etkinleþtirir. 
// Bu, endpoint'lerin (controller'lar gibi) tanýnmasýný saðlar.
app.UseRouting();

// 3. CORS politikasý. 
// Bu, Auth'dan ÖNCE gelmelidir ki OPTIONS istekleri 
// ve kimlik doðrulama yanýtlarý doðru CORS baþlýklarýný alabilsin.
app.UseCors(MyAllowSpecificOrigins);

// 4. Kimlik Doðrulama (Kullanýcý kim?)
app.UseAuthentication();

// 5. Yetkilendirme (Kullanýcý ne yapabilir?)
app.UseAuthorization();

// 6. Oturum (Session) 
// (Genellikle Auth/Authz'dan sonra, endpoint'lerden önce)
app.UseSession();

// 7. Endpoint'leri (controller'larý) eþleþtirir.
app.MapControllers();

//app.Run(async (context) => { Session });

app.Run();
