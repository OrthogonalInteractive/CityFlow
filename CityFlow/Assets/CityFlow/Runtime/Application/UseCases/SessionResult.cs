#nullable enable
namespace CityFlow.Application.UseCases
{
    public sealed class SessionResult
    {
        public int Wave { get; }
        public double SurvivalSeconds { get; }
        public long Delivered { get; }
        public string SourceId { get; }
        public SessionResult(int wave,double seconds,long delivered,string sourceId)
        { Wave=wave; SurvivalSeconds=seconds; Delivered=delivered; SourceId=sourceId; }
    }
}
