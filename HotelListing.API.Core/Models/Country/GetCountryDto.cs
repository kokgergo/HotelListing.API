using Microsoft.AspNetCore.SignalR;

namespace HotelListing.API.Core.Models.Country
{
    public class GetCountryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
    }
}
