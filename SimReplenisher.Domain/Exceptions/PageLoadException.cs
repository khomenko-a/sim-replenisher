using SimReplenisher.Domain.Enums;
using System.Xml;

namespace SimReplenisher.Domain.Exceptions
{
    public class PageLoadException : Exception
    {
        public Page Page { get; }
        public XmlDocument? ScreenDump { get; }

        public PageLoadException(Page page, XmlDocument screenDump) 
        {
            Page = page;
            ScreenDump = screenDump;
        }

        public PageLoadException(string message, Page page, XmlDocument screenDump)
            : base(message) 
        {
            Page = page;
            ScreenDump = screenDump;
        }

        public PageLoadException(string message, Exception inner, Page page, XmlDocument screenDump)
            : base(message, inner) 
        {
            Page = page;
            ScreenDump = screenDump;
        }
    }
}
