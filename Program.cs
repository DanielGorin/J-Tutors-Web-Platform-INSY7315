namespace J_Tutors_Web_Platform
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
            //PUBLIC
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=Info}/{id?}"); // INFO
            //pattern: "{controller=Home}/{action=Login}/{id?}"); // LOGIN
            //pattern: "{controller=Home}/{action=Register}/{id?}"); // REGSITER
            //pattern: "{controller=Home}/{action=Privacy}/{id?}"); // PRIVACY
            //----------------------------------------------------

            //USER:
            //----------------------------------------------------
            //pattern: "{controller=Home}/{action=UDashboard}/{id?}"); // USER DASHBOARD
            //pattern: "{controller=Home}/{action=UFileLibrary}/{id?}"); // USER FILE LIBRARY
            //pattern: "{controller=Home}/{action=UProfile}/{id?}"); // USER PROFILE
            //pattern: "{controller=Home}/{action=UEvents}/{id?}"); // USER EVENTS
            pattern: "{controller=Home}/{action=UEventHistory}/{id?}"); // USER EVENT HISTORY
            //pattern: "{controller=Home}/{action=UPointsLedger}/{id?}"); // USER POINTS LEDGER
            //pattern: "{controller=Home}/{action=UPointsLeaderboard}/{id?}"); // USER POINTS LEADERBOARD
            //pattern: "{controller=Home}/{action=UBooking}/{id?}"); // USER BOOKING
            //pattern: "{controller=Home}/{action=USessions}/{id?}"); // USER Sessions
            //----------------------------------------------------

            //ADMIN:
            //----------------------------------------------------

            //----------------------------------------------------

            app.Run();
        }
    }
}
