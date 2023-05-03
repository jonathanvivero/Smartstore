﻿#nullable enable

using Smartstore.Core.Common;
using Smartstore.Core.Localization;
using Smartstore.Core.Search.Facets;

namespace Smartstore.Core.Search
{
    public class SearchQuery : SearchQuery<SearchQuery>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchQuery"/> class without a search term being set
        /// </summary>
        public SearchQuery()
            : base((string[]?)null, null)
        {
        }

        public SearchQuery(
            string? field, 
            string? term, 
            SearchMode mode = SearchMode.Contains, 
            bool escape = false, 
            bool isFuzzySearch = false)
            : base(field.HasValue() ? new[] { field! } : null, term, mode, escape, isFuzzySearch)
        {
        }

        public SearchQuery(
            string[]? fields, 
            string? term, 
            SearchMode mode = SearchMode.Contains, 
            bool escape = false, 
            bool isFuzzySearch = false)
            : base(fields, term, mode, escape, isFuzzySearch)
        {
        }
    }

    public class SearchQuery<TQuery> : ISearchQuery where TQuery : class, ISearchQuery
    {
        private readonly Dictionary<string, FacetDescriptor> _facetDescriptors = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, object>? _customData;

        protected SearchQuery(
            string[]? fields, 
            string? term, 
            SearchMode mode = SearchMode.Contains, 
            bool escape = false, 
            bool isFuzzySearch = false)
        {
            Fields = fields;
            Term = term;
            Mode = mode;
            EscapeTerm = escape;
            IsFuzzySearch = isFuzzySearch;

            // TODO: (mg) Insufficient API design. TBD with MC.
            if (term.HasValue() && !fields.IsNullOrEmpty())
            {
                if (fields!.Length == 1)
                {
                    WithFilter(SearchFilter.BySearchTerm(fields![0], term, mode, escape));
                }
                else
                {
                    WithFilter(SearchFilter.Combined("searchterm",
                        fields!.Select(field => SearchFilter.BySearchTerm(field, term, mode, escape)).ToArray()));
                }
            }
        }

        // Language, Currency & Store
        public int? LanguageId { get; protected set; }
        public string? LanguageCulture { get; protected set; }
        public string? CurrencyCode { get; protected set; }
        public int? StoreId { get; protected set; }

        /// <summary>
        /// Specifies the fields to be searched.
        /// </summary>
        public string[]? Fields { get; set; }

        public string? Term { get; set; }

        /// <summary>
        /// A value indicating whether to escape the search term.
        /// </summary>
        public bool EscapeTerm { get; protected set; }

        /// <summary>
        /// Specifies the search mode.
        /// Note that the mode has an impact on the performance of the search. <see cref="SearchMode.ExactMatch"/> is the fastest,
        /// <see cref="SearchMode.StartsWith"/> is slower and <see cref="SearchMode.Contains"/> the slowest.
        /// </summary>
        public SearchMode Mode { get; protected set; }

        /// <summary>
        /// A value idicating whether to search by distance. For example "roam" finds "foam" and "roams".
        /// Only applicable if the search engine supports it. Note that a fuzzy search is typically slower.
        /// </summary>
        public bool IsFuzzySearch { get; protected set; }

        // Filtering
        public ICollection<ISearchFilter> Filters { get; } = new List<ISearchFilter>();

        // Facets
        public IReadOnlyDictionary<string, FacetDescriptor> FacetDescriptors => _facetDescriptors;

        // Paging
        public int Skip { get; protected set; }
        public int Take { get; protected set; } = int.MaxValue;
        public int PageIndex
        {
            get
            {
                if (Take == 0)
                    return 0;

                return Math.Max(Skip / Take, 0);
            }
        }

        // Sorting
        public ICollection<SearchSort> Sorting { get; } = new List<SearchSort>();

        // Spell checker
        public int SpellCheckerMaxSuggestions { get; protected set; }
        public int SpellCheckerMinQueryLength { get; protected set; } = 4;
        public int SpellCheckerMaxHitCount { get; protected set; } = 3;

        // Result control
        public SearchResultFlags ResultFlags { get; protected set; } = SearchResultFlags.WithHits;

        /// <summary>
        /// Gets the origin of the search. Examples:
        /// Search/Search: main catalog search page.
        /// Search/InstantSearch: catalog instant search.
        /// </summary>
        public string? Origin { get; protected set; }

        public IDictionary<string, object> CustomData => _customData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        #region Fluent builder

        public virtual TQuery HasStoreId(int id)
        {
            Guard.NotNegative(id);

            StoreId = id;

            return (this as TQuery)!;
        }

        public TQuery WithLanguage(Language language)
        {
            Guard.NotNull(language);
            Guard.NotEmpty(language.LanguageCulture, nameof(language.LanguageCulture));

            LanguageId = language.Id;
            LanguageCulture = language.LanguageCulture;

            return (this as TQuery)!;
        }

        public TQuery WithCurrency(Currency currency)
        {
            Guard.NotNull(currency);
            Guard.NotEmpty(currency.CurrencyCode);

            CurrencyCode = currency.CurrencyCode;

            return (this as TQuery)!;
        }

        public TQuery Slice(int skip, int take)
        {
            Guard.NotNegative(skip);
            Guard.NotNegative(take);

            Skip = skip;
            Take = take;

            return (this as TQuery)!;
        }

        /// <summary>
        /// Inits the spell check.
        /// </summary>
        /// <param name="maxSuggestions">Number of returned suggestions. 0 to disable spell check.</param>
        public TQuery CheckSpelling(int maxSuggestions, int minQueryLength = 4, int maxHitCount = 3)
        {
            Guard.IsPositive(minQueryLength);
            Guard.IsPositive(maxHitCount);

            if (maxSuggestions > 0)
            {
                ResultFlags |= SearchResultFlags.WithSuggestions;
            }
            else
            {
                ResultFlags &= ~SearchResultFlags.WithSuggestions;
            }

            SpellCheckerMaxSuggestions = Math.Max(maxSuggestions, 0);
            SpellCheckerMinQueryLength = minQueryLength;
            SpellCheckerMaxHitCount = maxHitCount;

            return (this as TQuery)!;
        }

        public TQuery WithTerm(
            string term,
            string fieldName,
            SearchMode mode = SearchMode.Contains,
            bool escape = false,
            bool isNotAnalyzed = false)
        {
            return WithFilter(SearchFilter.BySearchTerm(fieldName, term, mode, escape, isNotAnalyzed));
        }

        public TQuery WithFilter(ISearchFilter filter)
        {
            Guard.NotNull(filter);

            Filters.Add(filter);

            return (this as TQuery)!;
        }

        public TQuery SortBy(SearchSort sort)
        {
            Guard.NotNull(sort);

            Sorting.Add(sort);

            return (this as TQuery)!;
        }

        public TQuery BuildHits(bool build)
        {
            if (build)
            {
                ResultFlags |= SearchResultFlags.WithHits;
            }
            else
            {
                ResultFlags &= ~SearchResultFlags.WithHits;
            }


            return (this as TQuery)!;
        }

        /// <summary>
        /// Specifies whether facets are to be returned.
        /// Note that a search including facets is slower.
        /// </summary>
        public TQuery BuildFacetMap(bool build)
        {
            if (build)
            {
                ResultFlags |= SearchResultFlags.WithFacets;
            }
            else
            {
                ResultFlags &= ~SearchResultFlags.WithFacets;
            }


            return (this as TQuery)!;
        }

        public TQuery WithFacet(FacetDescriptor facetDescription)
        {
            Guard.NotNull(facetDescription);

            if (_facetDescriptors.ContainsKey(facetDescription.Key))
            {
                throw new InvalidOperationException("A facet description object with the same key has already been added. Key: {0}".FormatInvariant(facetDescription.Key));
            }

            _facetDescriptors.Add(facetDescription.Key, facetDescription);

            return (this as TQuery)!;
        }

        public TQuery OriginatesFrom(string origin)
        {
            Guard.NotEmpty(origin);

            Origin = origin;

            return (this as TQuery)!;
        }

        #endregion

        public override string ToString()
        {
            var str = string.Join(", ", Filters.Select(x => x.ToString()));

            if (IsFuzzySearch)
            {
                str += " (fuzzy)";
            }

            return str;
        }

        #region Utilities

        protected TQuery CreateFilter(string fieldName, params int[] values)
        {
            var len = values?.Length ?? 0;
            if (len > 0)
            {
                if (len == 1)
                {
                    return WithFilter(SearchFilter.ByField(fieldName, values![0]).Mandatory().ExactMatch().NotAnalyzed());
                }

                return WithFilter(SearchFilter.Combined(
                    fieldName, 
                    values!.Select(x => SearchFilter.ByField(fieldName, x).ExactMatch().NotAnalyzed()).ToArray()));
            }

            return (this as TQuery)!;
        }

        #endregion
    }
}
