/* Subtext/Config.cs

This file is part of the Subtext server.

Subtext is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Subtext is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with Subtext. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Collections.Generic;

namespace Subtext {
	
	// This should probably become a separate config file.
	public class Config {
		
		// Server name. Used to identify this server.
		public static string serverName = "subtext_testing";
		
		// If this is enabled, users will be locked until they are manually
		// verified by the admins.
		public static bool serverIsPrivate = false;
		
		// Size (in bytes) of secrets, salts, challenges, and responses
		// This should be AT LEAST 16. 32 should be more than enough.
		public static int secretSize = 32;
		
		// You probably shouldn't touch this.
		public static int pbkdf2Iterations = 10000;
		
		public static int passwordMinLength = 8;
		
		// Number of incorrect password attempts to allow before locking the account.
		public static int passwordMaxAttempts = 10;
		
		public static TimeSpan passwordLockTime = TimeSpan.FromHours(1);
		
		// Maximum amount of time an admin session can go without being renewed.
		// This should be very short (1-5 minutes) for optimal security.
		public static TimeSpan adminSessionDuration = TimeSpan.FromMinutes(2);
		
		// Maximum amount of time a user session can go without being renewed.
		public static TimeSpan sessionDuration = TimeSpan.FromMinutes(15);
		
		// SQL server connection options
		// Subtext uses Microsoft SQL Server by default. If you want to use
		// a different SQL server, you'll need to edit ConfigureServices()
		// in Startup.cs.
		public static string sqlServer = "localhost";
		public static string sqlDatabase = "Subtext";
		// This is a text file with the username on the first line, and the password on the second.
		public static string sqlCredsFile = "db.creds";
		
		// Maximum amount of results returned by a query.
		public static int pageSize = 500;
		
		// If true, the server will redirect all HTTP requests to HTTPS
		public static bool requireHttps = false;
		
		// End of configuration options
		
		public static string sqlUser;
		public static string sqlPassword;
		
		public static bool IsInit { get; private set; }
		
		public static void Init() {
			InitCreds();
			// TODO initialize more stuff?
			
			IsInit = true;
		}
		
		public static void InitCreds() {
			// Read SQL username and password from creds file
			using (StreamReader fh = new StreamReader(sqlCredsFile)) {
				sqlUser = fh.ReadLine().Trim();
				sqlPassword = fh.ReadLine().Trim();
			}
		}
		
	}
	
}
