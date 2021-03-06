#region LGPL license
/*
 * See the NOTICE file distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org.
 */
#endregion //license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Xml;
using ContentFiltering.Test.Util;
using ContentFiltering.Office.Word.Filters;
using XWiki.Office.Word;

namespace ContentFiltering.Test.Office.Word.Filters
{
    /// <summary>
    /// Test class for <code>WebImageAdaptorFilter</code>.
    /// </summary>
    [TestFixture]
    public class WebImageAdaptorFilterTest
    {
        private ConversionManager manager;
        private string initialHTML;
        private XmlDocument initialXmlDoc;


        /// <summary>
        /// Default constructor.
        /// </summary>
        public WebImageAdaptorFilterTest()
        {
            manager = ConversionManagerTestUtil.DummyConversionManager();
            initialHTML = "";
            initialXmlDoc = new XmlDocument();
        }
        /// <summary>
        /// Initialize the test.
        /// </summary>
        [TestFixtureSetUp]
        public void GlobalSetUp()
        {
            initialHTML = "<html  xmlns:v=\"urn:schemas-microsoft-com:vml\""
                + " xmlns:o=\"urn:schemas-microsoft-com:office:office\""
                + " xmlns:w=\"urn:schemas-microsoft-com:office:word\""
                + " xmlns:m=\"http://schemas.microsoft.com/office/2004/12/omml\""
                + " xmlns=\"http://www.w3.org/TR/REC-html40\">"
                + "<head><title>WebImageAdaptorFilterTest</title></head>"
                + "<body>"
                + "<img v:shapes=\"shape1\" alt=\"alternative text\" src=\"http://127.0.0.1:8080/xwiki/bin/download/Space1/Page1/image002.jpg\" />"
                +"</body></html>";

            //are atributul ImageInfo.XWORD_IMG_ATTRIBUTE
            //are atributul alt 
            //src-ul e file://
            //nu are attributul v:shapes
            initialXmlDoc.LoadXml(initialHTML);
        }

        /// <summary>
        /// Test method for web image adaptor filter. After filterning images should have <code>ImageInfo.XWORD_IMG_ATTRIBUTE</code> and 'alt' attributes,
        /// the 'src' should start with 'file://' and the 'v:shapes' attribute should not be present.
        /// </summary>
        [Test]
        public void TestFilter()
        {
            WebImageAdaptorFilter webImageAdaptorFilter = new WebImageAdaptorFilter(manager);
            webImageAdaptorFilter.Filter(ref initialXmlDoc);

            XmlNodeList images = initialXmlDoc.GetElementsByTagName("img");
            foreach (XmlNode image in images)
            {
                Assert.IsNotNull(image.Attributes[ImageInfo.XWORD_IMG_ATTRIBUTE]);
                Assert.AreEqual(image.Attributes["alt"].Value, "alternative text");
                Assert.IsTrue(image.Attributes["src"].Value.IndexOf("file://") >= 0);
                Assert.IsNull(image.Attributes["v:shapes"]);
            }
            
        }
    }
}
