// Need NuGet packages:
//  Microsoft.Data.SqlClient  (for SQL server interaction)
//  System.IO.Ports           (for "serial" port interaction)

using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.IO.Ports;
//using System.Windows.Forms; // for class SendKeys. Apparently not needed?

namespace Kulunvalvonta
{
    internal class Program
    {
        // In order to be cheapskate, status is of type TINYINT (8 bit unsigned) in the DB,
        // so we base this enum on byte here, too.
        [Flags]
        enum Status : byte
        {
            // 0 in the least bit means not logged in, i.e. logged out
            LoggedIn = 1 << 0,
            AutoLogOut = 1 << 1,
            LoggedByAdmin = 1 << 2
        }

        static string connectionString = string.Empty;
        static readonly string startMessage     = "Waiting for input from the RFID reader.\n";
        static readonly string goodNightMessage = "Closed for the night. Sleep tight. Zzz...\n";
        const ConsoleColor defaultColor = ConsoleColor.Gray;

        static readonly int openingHour = 6;
        static readonly int closingHour = 21;
        static bool closedForTheNight = false;

        static void TryOpenPort(SerialPort serialPort, string portName, bool rethrow = true)
        {
            try
            {
                serialPort.Open();
                if (serialPort.IsOpen)
                {
                    Print($"Successfully opened \"serial\" port {portName}.\n", ConsoleColor.Green);
                }
                else
                {
                    Print("WTF? The port was opened but it is not open!\nTILT!\n", ConsoleColor.DarkRed);
                    return;
                }
            }
            catch (Exception)
            {
                if (rethrow)
                {
                    Print($"Failed to open port {portName}!\nTILT!\n\n", ConsoleColor.DarkRed);
                    throw;
                }
                else
                {
                    Print($"Failed to open port {portName}. Please make sure the reader is plugged in.\nIf that doesn't help, restart this device.\n\n", ConsoleColor.DarkRed);
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                var rows = File.ReadLines("connectionString.txt");
                if (rows.Any())
                {
                    connectionString = rows.First().Trim();
                }
                else
                {
                    Print("Failed to read a complete line from file \"connectionString.txt\".\n", ConsoleColor.DarkRed);
                    return;
                }
            }
            catch (FileNotFoundException)
            {
                Print("Failed to open file \"connectionString.txt\".\n", ConsoleColor.DarkRed);
                return;
            }
            
            Console.Clear();
            
            int clrScrDelay;
            string portName;

            // if there are 2 args, use the second for clear screen delay.
            if ((args.Length > 1) && int.TryParse(args[1], out clrScrDelay))
            {
                Print($"Screen clear wait set to {clrScrDelay} ms.\n");
            }
            else
            {
                clrScrDelay = 20000; // in milliseconds
            }

            // use first argument, if existant, for COM port number
            if (args.Length > 0)
            {
                portName = $"COM{args[0]}";
                Print($"Attempting to use port {portName}.\n");
            }
            else
            {
                portName = "COM3";
                Print($"No COM port number provided as command line argument.\nAttempting to use default port {portName}.\n");
            }

            // Open the "serial" port.
            using (SerialPort serialPort = new SerialPort(portName))
            {
                // The TWN3 likes to use '\r' as line separator by default. We use the same
                // to minimize changes needed in the TWN's script.
                serialPort.NewLine = "\r";
                serialPort.ReadTimeout = clrScrDelay;

                TryOpenPort(serialPort, portName, true);

                Print("It is now ");
                Print($"{DateTime.Now}.\n", ConsoleColor.Cyan);
                Console.WriteLine();
                Print(startMessage);
                try
                {
                    SendKeys.SendWait("{F11}"); // go to full screen by "pressing" F11
                }
                catch (Exception e)
                {
                    Print("\nSome dumb error occurred while trying to go full screen:\n\n", ConsoleColor.DarkRed);
                    Print(e.ToString());
                    Console.WriteLine();
                }

                // Keep waiting for inputs and logging them in the DB.
                while (true)
                {
                    // if some fool's unplugged the reader, we need to
                    // reopen the port before we try reading from it
                    if (!serialPort.IsOpen)
                    {
                        TryOpenPort(serialPort, portName, false);
                        if (!serialPort.IsOpen)
                        {
                            // if failed, try again after 10 seconds
							Thread.Sleep(10000);
                            continue;
                        }
                    }

                    try
                    {
                        // this is where most of the time will be spent, waiting for uset input
                        string s = WaitForInputLoop(serialPort);

                        if (s.Length > 0)
                        {
                            int hour = DateTime.Now.Hour;
                            if ( hour >= closingHour || hour < openingHour )
                            {
                                Print("\nYou're not allowed to log in in the middle of the night.\n", ConsoleColor.DarkRed);
                                continue;
                            }
                            
                            LogUserInput(s);
                        }
                    }
                    catch (SqlException e)
                    {
                        Print("An SQL exception occurred:\n", ConsoleColor.DarkRed);
                        Console.WriteLine(e.Message);
                        Print("\nBetter luck next time?\n", ConsoleColor.Red);
                    }
                    catch (OperationCanceledException)
                    {
                        Print("\nSeems that there was probably a problem with the RFID reader.\n" +
                               "Suppressing the error for now. Make sure the reader is plugged in.\n\n", ConsoleColor.DarkRed);
                        Print("If that doesn't help, restart this device.\n\n", ConsoleColor.DarkRed);
                        Thread.Sleep(10000);
                    }
                    catch (Exception)
                    {
                        Print("\nAn unforeseen exception happened!\nTILT!\n\n", ConsoleColor.DarkRed);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Clears the screen and reprints the start message (self-evident)
        /// </summary>
        static void ResetScreen()
        {
            // Ja läl-läl-lää. Clear() ei toimi win11:ssa.
            // Pitää käyttää ylimääräistä taikasanaa.
            Console.Clear();

            if (OperatingSystem.IsWindows())
            {
                // Win11-kummallisuus.
                // TODO: Pitää vielä testata win kympissä.
                Console.WriteLine("\x1b[3J");

                // Set console buffer to equal the window in size,
                // to get rid on unaesthetic scroll bar.
                // tulee ArgumentOutOfRangeExceptionia, jos on kursori
                // kelaantunut alemmas kuin tuleva bufferin alalaita!
                Console.SetWindowPosition(0, 0);
                Console.SetCursorPosition(0, 0);
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
            }

            Print(DateTime.Now.ToString("f"), ConsoleColor.Cyan);
            Console.WriteLine();
            Print(startMessage);
        }

        /// <summary>
        /// Wait for a new message from SerialPort. If it takes too long,
        /// clear the screen and restart waiting.
        /// </summary>
        /// <remarks>
        /// Also responsible for checking if time passes 21, and logging
        /// everyone out who's still pretending to be logged in.
        /// </remarks>
        /// <returns>The string from SerialPort, trimmed</returns>
        static string WaitForInputLoop(SerialPort port)
        {
            while (true)
            {
                try
                {
                    string message = port.ReadLine();
                    return message.Trim();
                }
                catch (TimeoutException)
                {
                    // Sloppily using try-catch for flow control, because it's easier to let
                    // SerialPort.ReadLine() throw on timeout than to finagle with your own timer.
                }

                // putsataan ruutu, jos on odoteltu tarpeeksi kauan
                // paitsi yöllä, jotta automaattisesti uloslogatut pysysivät näkyvissä
                if ( !closedForTheNight )
                    ResetScreen();

                // Kun kello lyö 21:00, niin logata kaikki automaattisesti pihalle ja asettaa tuhma lippu
                int hour = DateTime.Now.Hour;
                //int hour = DateTime.Now.Minute;

                if ( !closedForTheNight && hour >= closingHour)
                {
                    closedForTheNight = true;
                    Console.Clear();
                    Print("Closing time!\n\n", ConsoleColor.Blue);

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string cmdStr = $"SELECT id, name, status FROM Users WHERE (status & {(int)Status.LoggedIn}) != 0;";
                        conn.Open();

                        // Can't use an SqlDataReader here because otherwise the ExecuteNonQuery() within
                        // MakeLogEntry() will whine about a datareader already being open, or something like that.
                        SqlDataAdapter ad = new SqlDataAdapter(cmdStr, conn);
                        DataTable tbl = new DataTable();
                        ad.Fill(tbl);

                        foreach (DataRow item in tbl.Rows)
                        {
                            int userId = (int)item[0];
                            string userName = (item[1] is DBNull) ? "[Unnamed]" : ((string)item[1]).Trim();
                            if (userName.Equals(""))
                                userName = "[Unnamed]";

                            var status = (Status)item[2];
                            status &= ~Status.LoggedIn;
                            status |= Status.AutoLogOut;

                            var date = MakeLogEntry(conn, userId, status);
                            Print($"{userName}", ConsoleColor.DarkYellow);
                            Print($" was automatically logged out on ");
                            Print($"{date}.\n", ConsoleColor.Cyan);
                        }
                    }

                    Console.WriteLine();
                    Print(goodNightMessage, ConsoleColor.Blue);
                }

                if ( openingHour <= hour && hour < closingHour )
                {
                    closedForTheNight = false;
                }
            }
        }

        /// <summary>Get id of the tag's user</summary>
        /// <returns>
        /// -1 if the tag wasn't in the DB<br />
        /// -2 if the tag is known but isn't assigned to anyone<br />
        /// the id otherwise (a positive number)
        /// </returns>
        static int GetUserID(SqlConnection conn, string tagId)
        {
            string cmdStr = "SELECT user_id FROM Tags WHERE rfid_id = @tag;";
            SqlCommand cmd = new SqlCommand(cmdStr, conn);
            cmd.Parameters.Add("@tag", System.Data.SqlDbType.Char);
            cmd.Parameters["@tag"].Value = tagId;
            var y = cmd.ExecuteScalar();

            if (y is null)
                return -1;

            if (y is DBNull)
                return -2;

            return (int)y;
        }

        /// <summary>Gets user status from DB.</summary>
        /// <returns>Tuple (name, country, status, success)<br />
        /// where the bool <i>success</i> denotes if the row corresponding
        /// to <i>userId</i> was successfully read from the DB.</returns>
        // i.e. how to return multiple values from a single function call using a tuple
        static (string, string, Status, bool) GetUserData(SqlConnection conn, int userId)
        {
            string cmdStr = "SELECT name, status, country FROM Users WHERE id = @user;";
            var cmd = new SqlCommand(cmdStr, conn);
            cmd.Parameters.Add("@user", System.Data.SqlDbType.Int);
            cmd.Parameters["@user"].Value = userId;
            string userName;
            string country;

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    country = (reader[2] is DBNull) ? "" : (string)reader[2];
                    userName = (reader[0] is DBNull) ? "[Unnamed]" : ((string)reader[0]).Trim();
                    if (userName.Equals(""))
                        userName = "[Unnamed]";

                    return (userName, country, (Status)reader[1], true);
                }
                else
                {
                    return ("", "", new Status(), false);
                }
            }
        }

        static DateTime? MakeLogEntry(SqlConnection conn, int userId, Status newStatus)
        {
            // Log the new event in the DB
            string cmdStr = "EXEC Update_Status @user, @newStatus";
            var cmd = new SqlCommand(cmdStr, conn);
            cmd.Parameters.Add("@user", System.Data.SqlDbType.Int);
            cmd.Parameters["@user"].Value = userId;
            cmd.Parameters.Add("@newStatus", System.Data.SqlDbType.TinyInt);
            cmd.Parameters["@newStatus"].Value = newStatus;

            // Oletan, että tämä heittää, jos jotain menee pieleen
            _ = cmd.ExecuteNonQuery();
            // Jos ei heittänyt, niin varmaankin logaus onnistui, ja voidaan jatkaa eteenpäin.

            // Get the official time of the event back from the DB and display it to the user.
            cmdStr = "SELECT date FROM Loggings WHERE event_id = (SELECT max(event_id) FROM Loggings);";
            cmd = new SqlCommand(cmdStr, conn);
            var y = cmd.ExecuteScalar();

            if ( y is null || y is DBNull )
                return null;
            else
                return (DateTime)y;
        }

        static void LogUserInput(string tagId)
        {
            Console.WriteLine();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                
                // tsekataan onko lätkä kenenkään käytössä
                int userId = GetUserID(conn, tagId);
                if (userId == -2)
                {
                    Print("This tag isn't assigned to anyone.\n", ConsoleColor.DarkRed);
                    return;
                }

                if (userId == -1)
                {
                    var name = DateTime.Now.ToString("O");
                    string cmdStr = $"INSERT INTO Tags(rfid_id, serial) VALUES(@tag, '{name}');";
                    var cmd = new SqlCommand(cmdStr, conn);
                    cmd.Parameters.Add("@tag", System.Data.SqlDbType.Char);
                    cmd.Parameters["@tag"].Value = tagId;
                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        Print("Previously unknown tag inserted into database.\nAssigned serial number: ");
                        Print($"{name}\n\n", ConsoleColor.DarkYellow);
                    }

                    Print("This tag isn't assigned to anyone!\n", ConsoleColor.DarkRed);
                    return;
                }

                // Read user name, status and country from DB.
                // Structured binding is called deconstructing in C#.
                var (userName, country, newStatus, success) = GetUserData(conn, (int)userId);
                if ( !success )
                {
                    Print("Failed to obtain user data related to this tag from the database.\n", ConsoleColor.DarkRed);
                    return;
                }

                // Calculate new status before entering it in the DB,
                // and make note if a message ought to be printed afterwards.
                newStatus ^= Status.LoggedIn;
                string inOrOut = (newStatus & Status.LoggedIn) != 0 ? "in" : "out";
                //Print(userName, ConsoleColor.DarkYellow);
                //Print($" logging {inOrOut}... ");
                bool notifyAboutAutoLogOut = (newStatus & Status.AutoLogOut) != 0;

                // clear all funny flags, so that normal operation is resumed after a normal log in/out
                newStatus &= Status.LoggedIn;

                // Log the new event in the DB
                var time = MakeLogEntry(conn, (int)userId, newStatus);

                // The greeting based on combination of in/out and nationality.
                bool countryError = false;
                string greeting;
                if ((newStatus & Status.LoggedIn) != 0)
                {
                    if      (country.Equals("fi")) greeting = " Tervetuloa.\n";
                    else if (country.Equals("se")) greeting = " Välkommen.\n";
                    else if (country.Equals("no")) greeting = " Velkommen.\n";
                    else
                    {
                        greeting = " Welcome.\n";
                        countryError = true;
                    }
                }
                else
                {
                    if      (country.Equals("fi")) greeting = " Näkemiin.\n";
                    else if (country.Equals("se")) greeting = " Hej då.\n";
                    else if (country.Equals("no")) greeting = " Ha det bra.\n";
                    else
                    {
                        greeting = " Goodbye.\n";
                        countryError = true;
                    }
                }

                // Display the official time of the event to the user.
                if ( time is not null )
                {
                    Print(userName, ConsoleColor.DarkYellow);
                    Print($" logged {inOrOut} on ");
                    Print($"{time:T}.", ConsoleColor.Cyan);
                    Print(greeting);
                }
                else
                {
                    Print(userName, ConsoleColor.DarkYellow);
                    Print($" logged {inOrOut}.");
                    Print(greeting);
                    Print("Somehow failed to get time and date from DB. WTF?\n", ConsoleColor.DarkRed);
                }

                if (countryError)
                {
                    Print("Faulty country entry in the database.\n", ConsoleColor.DarkRed);
                    return; // can't check for holidays without country info
                }

                if (notifyAboutAutoLogOut)
                {
                    Print("Seems like you were automatically logged out last time.\n", ConsoleColor.DarkRed);
                }

                Print("Your week so far:\n\n");
                WeeklyReport(conn, userId, country);
                Console.WriteLine();
            }
        }

        static void WeeklyReport(SqlConnection conn, int userId, string country)
        {
            // TODO: Tehdä tämä kunnolla, eikä tehdä älytöntä määrää tietokantakyselyjä

            DateOnly monday = GetMonday(DateTime.Now);
            TimeSpan[] norms = new TimeSpan[6];
            TimeSpan[] done = new TimeSpan[6];
            bool[] autoLogOuts = new bool[6];
            norms[5] = new(0, 0, 0);
            done[5] = new(0, 0, 0);
            autoLogOuts[5] = false;
            int daysSoFar = DateTime.Now.DayOfYear - monday.DayOfYear;
            TimeSpan normSoFar = TimeSpan.Zero;
            TimeSpan doneSoFar = TimeSpan.Zero;

            for (int i = 0; i < 5; ++i)
            {
                DateOnly day = monday.AddDays(i);
                norms[i] = CalculateNorm(conn, country, day, day);
                norms[5] += norms[i];
                (done[i], autoLogOuts[i]) = CalcAccumulatedTime(conn, userId, day, day.AddDays(1));
                done[5] += done[i];
                autoLogOuts[5] |= autoLogOuts[i];
                if (i <= daysSoFar)
                {
                    normSoFar += norms[i];
                    doneSoFar += done[i];
                }
            }

            //Print("      |  Mon  |  Tue  |  Wed  |  Thu  |  Fri  | Total\n------+-------+-------+-------+-------+-------+-------\n Goal");
            Print("      |   Mon   |   Tue   |   Wed   |   Thu   |   Fri   | Total\n------+---------+---------+---------+---------+---------+---------\n Goal");
            for (int i = 0; i < 6; ++i)
            {
                //Print($" | {24 * norms[i].Days + norms[i].Hours:D2}:{norms[i].Minutes:D2}");
                Print($" | {24 * norms[i].Days + norms[i].Hours:D2}h {norms[i].Minutes:D2}m");
            }
            Print("\n Done ");
            for (int i = 0; i < 6; ++i)
            {
                ConsoleColor color;
                if (i == 5)
                    color = ColorCodeTime(doneSoFar, normSoFar);
                else if (i > daysSoFar)
                    color = defaultColor;
                else
                    color = ColorCodeTime(done[i], norms[i]);

                Print("|");
                char space = autoLogOuts[i] ? '"' : ' ';
                //Print($"{space}{24 * done[i].Days + done[i].Hours:D2}:{done[i].Minutes:D2}{space}", color);
                Print($"{space}{24 * done[i].Days + done[i].Hours:D2}h {done[i].Minutes:D2}m{space}", color);
            }

            Print("\n");
        }

        static ConsoleColor ColorCodeTime(TimeSpan done, TimeSpan norm)
        {
            ConsoleColor color = ConsoleColor.Yellow;
            if (done >= norm)
                color = ConsoleColor.Green;
            else if (done < norm - TimeSpan.FromHours(2))
                color = ConsoleColor.DarkRed;

            return color;
        }

        /// <summary>Custom function for printing colorful text</summary>
        /// <remarks>Resets text color to <i>defaulColor</i> afterwards.</remarks>
        static void Print(string? str, ConsoleColor C = defaultColor)
        {
            if (str is not null)
            {
                if (C == ConsoleColor.Yellow)
                    Console.Write("\x1b[38;5;226m");
                else
                    Console.ForegroundColor = C;

                Console.Write(str);
                Console.ForegroundColor = defaultColor;
            }
        }

        /// <summary>Computes the Monday of the week of the input date</summary>
        static DateOnly GetMonday(DateTime input)
        {
            var date = DateOnly.FromDateTime(input);

            // the DayOfWeek enum starts the week with Sunday! Back up 1 day to avoid going the wrong way.
            if (date.DayOfWeek == DayOfWeek.Sunday)
                date = date.AddDays(-1);

            return date.AddDays(DayOfWeek.Monday - date.DayOfWeek);
         }

        static (TimeSpan, bool) CalcAccumulatedTime(SqlConnection conn, int userId, DateOnly startDate, DateOnly endingDate)
        {
            string cmdStr = $"SELECT new_status, date FROM Loggings WHERE date >= '{startDate.ToString(DateTimeFormatInfo.InvariantInfo)}' and date <= '{endingDate.ToString(DateTimeFormatInfo.InvariantInfo)}' and user_id = {userId} ORDER BY date;";
            SqlCommand cmd = new SqlCommand(cmdStr, conn);
            TimeSpan time = new TimeSpan(0);
            bool kicked = false, wasNaughty = false;

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                DateTime start = new();
                bool loggedIn = false;
                
                while (reader.Read())
                {
                    var status = (Status)reader[0];
                    var date = (DateTime)reader[1];

                    if ((status & Status.LoggedIn) != 0) // log in event
                    {
                        if (!loggedIn)
                        {
                            start = date;
                            loggedIn = true;
                        }
                    }
                    else if (loggedIn) // log out event
                    {
                        time += date - start;
                        loggedIn = false;
                        kicked |= !wasNaughty && (status & Status.AutoLogOut) != 0;
                    }

                    wasNaughty = (status & Status.AutoLogOut) != 0;
                }
            }

            return (time, kicked);
        }

        static TimeSpan CalculateNorm(SqlConnection conn, string country, DateOnly first, DateTime last)
        {
            return CalculateNorm(conn, country, first, DateOnly.FromDateTime(last));
        }

        static TimeSpan CalculateNorm(SqlConnection conn, string country, DateOnly first, DateOnly last)
        {
            const int mondayMinutes = 7 * 60 + 50;
            const int fridayMinutes = 6 * 60 + 10;
            int norm = 0; // in minutes
            
            // add up required minutes for the interval
            for (DateOnly day = first; day <= last; day = day.AddDays(1))
            {
                norm += day.DayOfWeek switch
                {
                    DayOfWeek.Friday => fridayMinutes,
                    DayOfWeek.Saturday => 0,
                    DayOfWeek.Sunday => 0,
                    _ => mondayMinutes
                };
            }

            // apply deductions
            // country here is the name of the column in the Holidays table, meaning the deduction in minutes
            string cmdStr = $"SELECT day, {country} FROM Holidays WHERE day >= '{first.ToString(DateTimeFormatInfo.InvariantInfo)}' and day <= '{last.ToString(DateTimeFormatInfo.InvariantInfo)}' and {country} != 0;";
            SqlCommand cmd = new SqlCommand(cmdStr, conn);
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var holiday = (DateTime)reader[0];
                    var deduction = (int)reader[1];

                    if (holiday.DayOfWeek == DayOfWeek.Sunday || holiday.DayOfWeek == DayOfWeek.Saturday)
                        continue;

                    // negative number signifies that the whole day is off,
                    // non-negative the amount of minutes it's shorter than usual
                    if ( deduction < 0 )
                        norm -= (holiday.DayOfWeek == DayOfWeek.Friday) ? fridayMinutes : mondayMinutes;
                    else
                        norm -= deduction;
                }
            }

            return TimeSpan.FromMinutes(norm);
        }
    }
}
