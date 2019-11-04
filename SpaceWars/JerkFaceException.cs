using System;
using System.Runtime.Serialization;

namespace SpaceWars
{
    [Serializable]
    internal class JerkFaceException : Exception
    {
        public JerkFaceException()
        {
        }

        public JerkFaceException(string message) : base(message)
        {
        }

        public JerkFaceException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected JerkFaceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}