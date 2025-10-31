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

            builder.Services.AddSingleton<BlobStorageService>();

            builder.Services.AddScoped<UserProfileService>();
            builder.Services.AddScoped<UserLeaderboardService>();
            builder.Services.AddScoped<UserLedgerService>();
            builder.Services.AddScoped<UserBookingService>();
            builder.Services.AddScoped<AdminAgendaService>();


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

            builder.Services.AddSingleton<EventService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("AzureSql");
                return new EventService(connectionString);
            });

            builder.Services.AddSingleton<FileShareService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("AzureSql");
                return new FileShareService(configuration, connectionString);
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
            //pattern: "{controller=Test}/{action=FileShare}/{id?}"); // TEST FILE SHARE DEMO - WORKS
            //pattern: "{controller=TestBlob}/{action=Gallery}/{id?}"); // BLOB DEMO - WORKS
            //----------------------------------------------------

            //PUBLIC
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=Info}/{id?}"); // INFO - FUNCTIONAL
            //pattern: "{controller=Home}/{action=Login}/{id?}"); // LOGIN - FUNCTIONAL
            pattern: "{controller=Home}/{action=AdminLogin}/{id?}"); // ADMINLOGIN - non FUNCTIONAL (needs to be differentiated)
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
            //pattern: "{controller=Home}/{action=UBooking}/{id?}"); // USER BOOKING - (close)
            //pattern: "{controller=Home}/{action=USessions}/{id?}"); // USER SESSIONS - (close)
            //----------------------------------------------------

            //ADMIN:
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=ADashboard}/{id?}"); // ADMIN DASHBOARD - close
            //pattern: "{controller=Home}/{action=ASessionsCalendar}/{id?}"); // ADMIN SESSIONS CALENDAR - close
            //pattern: "{controller=Home}/{action=AUserList}/{id?}"); // ADMIN USER LIST - FUNCTIONAL (no sort)
            //pattern: "{controller=Home}/{action=AUserDetails}/{id?}"); // ADMIN USER DETAILS
            //pattern: "{controller=Home}/{action=AEventList}/{id?}"); // ADMIN EVENT LIST
            //pattern: "{controller=Home}/{action=AEventDetails}/{id?}"); // ADMIN EVENT DETAILS
            //pattern: "{controller=Home}/{action=AFiles}/{id?}"); // ADMIN FILES MANAGEMENT
            //pattern: "{controller=Home}/{action=APricing}/{id?}"); // ADMIN PRICE MANAGEMENT - FUNCTIONAL
            //pattern: "{controller=Home}/{action=ALeaderboard}/{id?}"); // ADMIN LEADERBOARD
            //pattern: "{controller=Home}/{action=AAnalytics}/{id?}"); // ADMIN ANALYTICS
            //pattern: "{controller=Home}/{action=AAccount}/{id?}"); // ADMIN ACCOUNT - FUNCTIONAL (light and dark brocken)







            //----------------------------------------------------

            app.Run();
        }
    }
}
