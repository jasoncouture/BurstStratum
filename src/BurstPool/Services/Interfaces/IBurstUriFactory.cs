using System;
using Microsoft.AspNetCore.Http;

namespace BurstPool.Services.Interfaces
{
    public interface IBurstUriFactory
    {
        Uri GetUri(string requestType, PathString pathString = default(PathString), QueryString queryString = default(QueryString));
    }
}