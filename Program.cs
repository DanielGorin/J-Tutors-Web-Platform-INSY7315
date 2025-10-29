using J_Tutors_Web_Platform.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using J_Tutors_Web_Platform.Services.Storage;


namespace J_Tutors_Web_Platform
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<FileShareService>();
            builder.Services.AddSingleton<BlobStorageService>();

            builder.Services.AddSingleton<FileShareService>();

            builder.Services.AddScoped<UserProfileService>();
            builder.Services.AddScoped<UserLeaderboardService>();
            builder.Services.AddScoped<UserLedgerService>();

            //Adding AuthService as a singleton service, and configuring it with the Azure SQL connection string from appsettings.json
            builder.Services.AddSingleton<AuthService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("AzureSql");
                return new AuthService(connectionString);
            });

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => 
            {
                options.LoginPath = "/Home/Login"; // Redirect to login page if not authenticated
                options.LogoutPath = "/Home/Login"; // Redirect to logout page
            });

            builder.Services.AddSingleton<AdminService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("AzureSql");
                return new AdminService(connectionString);
            });

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
            //Testing
            //----------------------------------------------------
            pattern: "{controller=Test}/{action=FileShare}/{id?}"); // TEST FILE SHARE DEMO
            //pattern: "{controller=TestBlob}/{action=Gallery}/{id?}"); // BLOB DEMO
            //----------------------------------------------------

            //PUBLIC
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=Info}/{id?}"); // INFO - FUNCTIONAL
            //pattern: "{controller=Home}/{action=Login}/{id?}"); // LOGIN - FUNCTIONAL
            //pattern: "{controller=Home}/{action=AdminLogin}/{id?}"); // ADMINLOGIN - non FUNCTIONAL
            //pattern: "{controller=Home}/{action=Register}/{id?}"); // REGSITER - FUINCITONAL
            //----------------------------------------------------

            //USER:
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=UDashboard}/{id?}"); // USER DASHBOARD
            //pattern: "{controller=Home}/{action=UFileLibrary}/{id?}"); // USER FILE LIBRARY
            //pattern: "{controller=Home}/{action=UProfile}/{id?}"); // USER PROFILE - FUNCTIONAL
            //pattern: "{controller=Home}/{action=UEvents}/{id?}"); // USER EVENTS
            //pattern: "{controller=Home}/{action=UEventHistory}/{id?}"); // USER EVENT HISTORY
            //pattern: "{controller=Home}/{action=UPointsLedger}/{id?}"); // USER POINTS LEDGER - FUNCTIONAL
            //pattern: "{controller=Home}/{action=UPointsLeaderboard}/{id?}"); // USER POINTS LEADERBOARD - FUNCTIONAL
            //pattern: "{controller=Home}/{action=UBooking}/{id?}"); // USER BOOKING
            //pattern: "{controller=Home}/{action=USessions}/{id?}"); // USER SESSIONS
            //----------------------------------------------------

            //ADMIN:
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=ADashboard}/{id?}"); // ADMIN DASHBOARD
            //pattern: "{controller=Home}/{action=ASessionsCalendar}/{id?}"); // ADMIN SESSIONS CALENDAR
            //pattern: "{controller=Home}/{action=AUserList}/{id?}"); // ADMIN USER LIST
            //pattern: "{controller=Home}/{action=AUserDetails}/{id?}"); // ADMIN USER DETAILS
            //pattern: "{controller=Home}/{action=AEventList}/{id?}"); // ADMIN EVENT LIST
            //pattern: "{controller=Home}/{action=AEventDetails}/{id?}"); // ADMIN EVENT DETAILS
            //pattern: "{controller=Home}/{action=AFiles}/{id?}"); // ADMIN FILES MANAGEMENT
            //pattern: "{controller=Home}/{action=APricing}/{id?}"); // ADMIN PRICE MANAGEMENT
            //pattern: "{controller=Home}/{action=ALeaderboard}/{id?}"); // ADMIN LEADERBOARD
            //pattern: "{controller=Home}/{action=AAnalytics}/{id?}"); // ADMIN ANALYTICS
            //pattern: "{controller=Home}/{action=AAccount}/{id?}"); // ADMIN ACCOUNT







            //----------------------------------------------------

            app.Run();
        }
    }
}
