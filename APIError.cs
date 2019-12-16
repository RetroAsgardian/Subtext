/* Subtext/APIError.cs

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

namespace Subtext {
	[Serializable]
	public class APIError {
		public string error;
		public string Error { get { return this.error; } }
		
		public APIError(string error) {
			// Console.WriteLine("APIError initialized");
			this.error = error;
		}
	}
}
