using System;

namespace Subtext {
	[Serializable]
	public class APIError {
		public string error;
		public string Error { get { return this.error; } }
		
		public APIError(string error) {
			Console.WriteLine("APIError initialized");
			this.error = error;
		}
	}
}
