using System;
using System.Runtime.Serialization;

namespace GeneratorDanych
{
	[Serializable]
	public class GeneratorDanychException : Exception
	{
		public GeneratorDanychException()
		{
		}

		public GeneratorDanychException(string message) : base(message)
		{
		}

		public GeneratorDanychException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected GeneratorDanychException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}