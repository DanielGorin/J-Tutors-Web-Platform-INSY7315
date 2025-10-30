
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace J_Tutors_Web_Platform.Services
{
    public class AuthService //everything to do with logging in and registering
    {
        private readonly string _connectionString;
        public AuthService(string connectionString)
        {
            _connectionString = connectionString;
        }

        //public string UpdateLastActive(string Username)
        //{
        //    const string sql = "update Users set LastActive = @LastActive where Username = @Username";
        //    using var constring = new SqlConnection(_connectionString);
        //    using var cmd = new SqlCommand(sql, constring);
        //    cmd.Parameters.AddWithValue("@LastActive", DateTime.UtcNow);
        //    cmd.Parameters.AddWithValue("@Username", Username);
        //    constring.Open();
        //    cmd.ExecuteNonQuery();

        //    return "Great Success";
        //}

        public string Login(string Username, string Password) //can be changed to username and email later on for now just uses usernmae.
        {
            const string sql = "select * from Users where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            //reading through returned data
            while (reader.Read())
            {
                string storedHash = reader["PasswordHash"].ToString();
                string storedSalt = reader["PasswordSalt"].ToString();

                if (VerifyPassword(Password, storedHash, storedSalt))
                {
                    return "Login Successful";
                }
            }

            return "Incorrect username or password";

        }

        public string AdminLogin(string Username, string Password) //can be changed to username and email later on for now just uses usernmae.
        {
            const string sql = "select * from Admins where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            //reading through returned data
            while (reader.Read())
            {
                string storedHash = reader["PasswordHash"].ToString();
                string storedSalt = reader["PasswordSalt"].ToString();

                if (VerifyPassword(Password, storedHash, storedSalt))
                {
                    Console.WriteLine("Admin login successful");
                    return "Login Successful";
                }
            }

            Console.WriteLine("Admin login failed");

            return "Incorrect username or password";

        }

        public string ChangeAdminPassword(string Username, string NewPassword) 
        {
            string salt = GetSalt(Username);
            string hashedPassword = HashPassword(NewPassword, salt);

            const string sql = "update Admins set PasswordHash = @PasswordHash where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();

            cmd.ExecuteNonQuery();

            return "Password changed successfully";
        }

        public string GetSalt(string Username) 
        {
            const string sql = "select PasswordSalt from Admins where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);
            cmd.Parameters.AddWithValue("@Username", Username);
            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();
            //reading through returned data
            while (reader.Read())
            {
                string storedSalt = reader["PasswordSalt"].ToString();
                return storedSalt;
            }
            return "";
        }

        public string Register(string Email, string Username, string Password, string ConfirmPassword, string Phone, DateOnly BirthDate, string ThemePreference, string SubjectInterest, string FirstName, string Surname) 
        {
            //checking if username already exists in database
            if (IsUsernameTaken(Username))
            {
                Console.WriteLine("User registered successfully");
                return "Username already taken";
            }

            //checking if email already exists in database
            if (IsEmailTaken(Email)) 
            {
                Console.WriteLine("Email already in use");
                return "Email already in use";
            }

            //checking if both entered passwords match, can likely be done on frontend, this is incase it is not done that way
            if (Password != ConfirmPassword) 
            {
                Console.WriteLine("Passwords do not match");
                return "Passwords do not match";
            }

            string salt = GenerateSalt();

            string hashedPassword = HashPassword(Password, salt);

            DateOnly RegistrationDate = DateOnly.FromDateTime(DateTime.Now);

            //inserting new user into database using sqlddddd
            const string sql = "insert into Users (Email, Username, PasswordHash, PasswordSalt, Phone, BirthDate, RegistrationDate, ThemePreference, LeaderboardVisible, SubjectInterest, FirstName, Surname) " +
                               "values (@Email, @Username, @PasswordHash, @PasswordSalt, @Phone, @BirthDate, @RegistrationDate, @ThemePreference, @LeaderboardVisible, @SubjectInterest, @FirstName, @Surname)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            //adding parameters to prevent sql injection
            cmd.Parameters.AddWithValue("@Email", Email);
            cmd.Parameters.AddWithValue("@Username", Username);
            cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
            cmd.Parameters.AddWithValue("@PasswordSalt", salt);
            cmd.Parameters.AddWithValue("@Phone", Phone);
            cmd.Parameters.AddWithValue("@BirthDate", BirthDate);
            cmd.Parameters.AddWithValue("@RegistrationDate", RegistrationDate);
            cmd.Parameters.AddWithValue("@ThemePreference", "light-theme");
            cmd.Parameters.AddWithValue("@LeaderboardVisible", 0);
            cmd.Parameters.AddWithValue("@SubjectInterest", SubjectInterest);
            cmd.Parameters.AddWithValue("@FirstName", FirstName);
            cmd.Parameters.AddWithValue("@Surname", Surname);

            //executing the command
            constring.Open();
            cmd.ExecuteNonQuery();

            Console.WriteLine("User registered successfully");
            return "Successfully created account";

        }

        //Login Logic
        public bool VerifyPassword(string Password, string PasswordHash, string PasswordSalt) 
        {
            //checking if the entered password is correct by taking entered password and adding the salt from the database, then comparing it to the hash from the database
            if (HashPassword(Password, PasswordSalt) == PasswordHash) 
            {
                return true;
            }
            else 
            {
                return false;
            }
        }

        //Register Logic
        public bool IsUsernameTaken(string Username) //does not exist yet
        {
            //logic to check if username exists in db
            const string sql = "select count(*) from Users where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();

            //if username count returns more than 0 then IsTaken is true, else false
            if ((int)cmd.ExecuteScalar() > 0) 
            {
                return true;
            }
            else 
            {
                return false;
            }
        }

        //Register Logic
        public bool IsEmailTaken(string Email) //does not exist yet
        {
            //logic to check if email exists in db
            const string sql = "select count(*) from Users where Email = @Email";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Email", Email);

            constring.Open();

            //if email count returns more than 0 then IsTaken is true, else false
            if ((int)cmd.ExecuteScalar() > 0) 
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //Register Logic
        public string GenerateSalt(int size = 32) 
        {
            //generating a random array of bytes which will be used as a salt for hashing passwords
            byte[] saltBytes = new byte[size];
            using (var rng = RandomNumberGenerator.Create()) 
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        //Login and Register Logic
        public string HashPassword(string password, string salt) 
        {
            string Hash;

            //Using SHA256 for hashing
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] PnS = Encoding.UTF8.GetBytes(password + salt); //adding salt to password and converting to bytes
                byte[] bytes = sha256.ComputeHash(PnS); //hashing the password+salt
                Hash = Convert.ToBase64String(bytes); //converting to string for storage
            }

            return Hash;
        }
    }
}
