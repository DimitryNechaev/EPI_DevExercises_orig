﻿using AlloyTraining.Business.ExtensionMethods;
using AlloyTraining.Models.Pages;
using AlloyTraining.Models.ViewModels;
using EPiServer;
using EPiServer.Core;
using EPiServer.Filters;
using EPiServer.Search;
using EPiServer.Search.Queries.Lucene;
using EPiServer.Security;
using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace AlloyTraining.Controllers
{
    public class SearchPageController : PageControllerBase<SearchPage>
    {
        public readonly SearchHandler searchHandler;

        public SearchPageController(IContentLoader loader, SearchHandler searchHandler) : base(loader)
        {
            this.searchHandler = searchHandler;
        }

        public ActionResult Index(SearchPage currentPage, string q)
        {
            var viewmodel = new SearchPageViewModel(currentPage);

            viewmodel.StartPage = loader.Get<StartPage>(ContentReference.StartPage);

            viewmodel.MenuPages = FilterForVisitor.Filter(
                loader.GetChildren<SitePageData>(ContentReference.StartPage))
                .Cast<SitePageData>().Where(page => page.VisibleInMenu);

            viewmodel.Section = currentPage.ContentLink.GetSection();

            if (!string.IsNullOrWhiteSpace(q))
            {
                // 1. only pages
                var onlyPages = new ContentQuery<SitePageData>();

                // 2. free-text query
                var freeText = new FieldQuery(q);

                // 3. only what the current user can read
                var readAccess = new AccessControlListQuery();
                readAccess.AddAclForUser(PrincipalInfo.Current, HttpContext);

                // 4. only under the Start page (to exclude Wastebasket, for example)
                var underStart = new VirtualPathQuery();
                underStart.AddContentNodes(ContentReference.StartPage);

                // build the query from the expressions
                var query = new GroupQuery(LuceneOperator.AND);
                query.QueryExpressions.Add(freeText);
                query.QueryExpressions.Add(onlyPages);
                query.QueryExpressions.Add(readAccess);
                query.QueryExpressions.Add(underStart);

                // get the first page of ten results
                SearchResults results = searchHandler.GetSearchResults(query, 1, 10);

                viewmodel.SearchText = q;

                viewmodel.SearchResults = results.IndexResponseItems
                    .Select(item => new Result
                    {
                        Title = item.Title,
                        Description = item.DisplayText?.TruncateAtWord(20),
                        Url = ConvertToUri(item).ToString()
                    }).ToList();
            }
            return View(viewmodel);
        }

        private Uri ConvertToUri(IndexResponseItem item)
        {
            try
            {
                var url = new UrlBuilder(item.Uri);
                Global.UrlRewriteProvider.ConvertToExternal(url, item, Encoding.UTF8);
                return url.Uri;
            }
            catch
            {
                return default(Uri);
            }
        }
    }
}