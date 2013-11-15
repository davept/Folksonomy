using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MvcApplication1.Domain.Models.ResultSets;
using MvcApplication1.Domain.Utilities;

namespace MvcApplication1.Infrastructure.CustomHelpers
{
    /// <summary>
    /// 
    /// </summary>
    public static class Folksonomy
    {
        private const string linkTitle = "Blog posts for {0}";

        public enum Scaling
        {
            Linear = 0,
            Logarithmic
        }

        public enum OrderingType
        {
            None = 0,
            Alphabetic,
            Ascending,
            Descending,
            Balanced,
            Random
        }

        public enum ColourCorrelation
        {
            Index = 0,
            Count
        }

        public static MvcHtmlString TagIndex(this HtmlHelper html, IEnumerable<CountTagUse> tagData, Func<string, string> urlGenerator,
            OrderingType order = OrderingType.None, int topNItems = -1, string controlId = "tagIndex")
        {
            if (tagData == null || tagData.Count() == 0)
                return new MvcHtmlString(string.Empty);

            if (topNItems > 0)
                tagData = SortData(tagData, OrderingType.Descending).Take(topNItems);

            if (order != OrderingType.None && (topNItems <= 0 || order != OrderingType.Descending))
                tagData = SortData(tagData, order);

            TagBuilder tagUl = new TagBuilder("ul");
            tagUl.MergeAttribute("id", controlId);
            tagUl.AddCssClass("tagIndex");

            foreach (CountTagUse ctu in tagData)
            {
                TagBuilder tagSpan = new TagBuilder("span");
                tagSpan.SetInnerText(string.Format("({0})", ctu.Count.ToString()));

                TagBuilder tagLi = new TagBuilder("li");
                tagLi.InnerHtml =
                    Link(urlGenerator(ctu.Tag), ctu.Tag)
                    + tagSpan.ToString();

                tagUl.InnerHtml += tagLi.ToString();
            }

            return new MvcHtmlString(tagUl.ToString());
        }

        public static MvcHtmlString TagCloud(this HtmlHelper html, IEnumerable<CountTagUse> tagData, Func<string, string> urlGenerator,
            OrderingType order = OrderingType.None, Scaling scalingMethod = Scaling.Linear, int topNItems = -1, int gradations = 6, string controlId = "tagCloud")
        {
            if (tagData == null || tagData.Count() == 0)
                return new MvcHtmlString(string.Empty);

            if (topNItems > 0)
                tagData = SortData(tagData, OrderingType.Descending).Take(topNItems);

            if (order != OrderingType.None && (topNItems <= 0 || order != OrderingType.Descending))
                tagData = SortData(tagData, order);

            int minCount,
                maxCount;

            MinAndMax(tagData, out minCount, out maxCount);
            
            double step;
            if (scalingMethod == Scaling.Linear)
            {
                step = (maxCount - minCount) / (gradations - 1);
            }
            else
            {
                step = Math.Log10(maxCount - minCount) / (gradations - 1);
            }

            TagBuilder tagUl = new TagBuilder("ul");
            tagUl.MergeAttribute("id", controlId);
            tagUl.AddCssClass("tagCloud");

            foreach (CountTagUse ctu in tagData)
            {
                tagUl.InnerHtml += TagCloudItem(Math.Max(ctu.Count - minCount, 1), scalingMethod, step, ctu.Tag, urlGenerator(ctu.Tag));
            }

            return new MvcHtmlString(tagUl.ToString());
        }

        private static string TagCloudItem(int count, Scaling scalingMethod, double step, string tag, string url)
        {
            string weighting;

            if (scalingMethod == Scaling.Linear)
            {
                weighting = Math.Round(count / step + 1).ToString();
            }
            else
            {
                weighting = Math.Round((Math.Log10(count) / step + 1)).ToString();
            }

            TagBuilder tagLi = new TagBuilder("li");
            tagLi.InnerHtml = Link(url, tag, "weight" + weighting);

            return tagLi.ToString();
        }

        public static MvcHtmlString HeatMap(this HtmlHelper html, IEnumerable<CountTagUse> tagData, Func<string, string> urlGenerator,
            ColourCorrelation correlation = ColourCorrelation.Index, bool showCount = true, int topNItems = -1, double saturation = 1,
            double luminance = 0.5, bool taperWidth = true, int minWidthPercentage = 50, string controlId = "heatMap")
        {
            if (tagData == null || tagData.Count() == 0)
                return new MvcHtmlString(string.Empty);

            tagData = SortData(tagData, OrderingType.Descending);

            if (topNItems > 0)
                tagData = tagData.Take(topNItems);

            int gradations = tagData.Count(),
                minCount = 0,
                maxCount = 0,
                range = 0;

            double stepHue = 0,
                widthRange = 100 - minWidthPercentage,
                stepWidth = 0,
                liWidth = 100;

            HSLColor heatMap = null;

            if (correlation == ColourCorrelation.Count)
            {
                MinAndMax(tagData, out minCount, out maxCount);
                
                range = maxCount - minCount;
            }
            else
            {
                //This class stores Hue as 0-1 not degrees 0 - 360. We want half the colour wheel from red to pale blue which is at 180 degrees.
                //http://upload.wikimedia.org/wikipedia/commons/a/ad/HueScale.svg
                stepHue = 0.5 / gradations;
                stepWidth = widthRange / gradations;
                liWidth = 100 + stepWidth;
                heatMap = new HSLColor(0, saturation, luminance);
            }

            TagBuilder mapUl = new TagBuilder("ul");
            mapUl.MergeAttribute("id", controlId);
            mapUl.AddCssClass("heatMap");

            foreach (CountTagUse ctu in tagData)
            {
                string colour;

                if (correlation == ColourCorrelation.Count)
                {
                    double countRatio = (double)(ctu.Count - minCount) / range;
                    heatMap = new HSLColor(0.5 - 0.5 * countRatio, saturation, luminance);
                    colour = heatMap.ToHexRgbString();

                    if (taperWidth)
                        liWidth = minWidthPercentage + widthRange * countRatio;
                }
                else
                {
                    colour = heatMap.ToHexRgbString();
                    heatMap.Hue += stepHue;

                    if (taperWidth)
                        liWidth -= stepWidth;
                }

                mapUl.InnerHtml += HeatMapItem(
                    ctu.Tag,
                    urlGenerator(ctu.Tag),
                    String.Format("background: {0}; width: {1}%", colour, ((int)liWidth).ToString()),
                    showCount,
                    ctu.Count.ToString());
            }

            return new MvcHtmlString(mapUl.ToString());
        }

        private static string HeatMapItem(string tag, string url, string htmlItemStyle, bool showCount, string htmlCount)
        {
            TagBuilder tagLi = new TagBuilder("li");
            tagLi.MergeAttribute("style", htmlItemStyle);
            tagLi.InnerHtml = Link(url, tag);

            if (showCount)
            {
                TagBuilder countSpan = new TagBuilder("span");
                countSpan.MergeAttribute("class", "txHmTxt");

                TagBuilder countA = new TagBuilder("a");
                countA.SetInnerText(htmlCount);

                TagBuilder triangleSpan = new TagBuilder("span");
                triangleSpan.MergeAttribute("class", "txHmTri");

                countSpan.InnerHtml += countA.ToString()
                    + triangleSpan.ToString();

                tagLi.InnerHtml += countSpan.ToString();
            }

            return tagLi.ToString();
        }

        public static MvcHtmlString ParallelTagCloud(this HtmlHelper html)
        {
            throw new System.NotImplementedException();
        }

        private static string Link(string href, string anchor, string cssClass = "")
        {
            return Link(string.Format(linkTitle, anchor), href, anchor, cssClass);
        }

        private static string Link(string title, string href, string anchor, string cssClass)
        {
            TagBuilder link = new TagBuilder("a");
            link.SetInnerText(anchor);

            if (!string.IsNullOrEmpty(cssClass))
            {
                link.AddCssClass(cssClass);
            }

            link.MergeAttributes<string, string>(new Dictionary<string, string>
            {
                { "href", href },
                { "title", title },
                { "rel", "tag" }
            });

            return link.ToString();
        }

        private static IEnumerable<CountTagUse> SortData(IEnumerable<CountTagUse> tagData, OrderingType order)
        {
            switch (order)
            {
                case OrderingType.Alphabetic:
                    return tagData.OrderBy(x => x.Tag);

                case OrderingType.Ascending:
                    return tagData.OrderBy(x => x.Count);

                case OrderingType.Descending:
                    return tagData.OrderBy(x => -x.Count);

                case OrderingType.Balanced:
                    var sorted = tagData.OrderBy(x => x.Count);
                    return sorted.Where((_, index) => index % 2 == 0).Concat(sorted.Where((_, index) => index % 2 == 1).Reverse());

                case OrderingType.Random:
                    var randomized = tagData.ToList();
                    randomized.Shuffle();
                    return randomized;

                case OrderingType.None:
                    return tagData;

                default:
                    return tagData;
            }
        }
        
        private static void MinAndMax(IEnumerable<CountTagUse> tagData, out int min, out int max)
        {
            int minCount = int.MaxValue,
                maxCount = int.MinValue;

            foreach (var ctu in tagData)
            {
                minCount = Math.Min(minCount, ctu.Count);
                maxCount = Math.Max(maxCount, ctu.Count);
            }

            min = minCount;
            max = maxCount;
        }
    }
}