using Tutorial9.Model;

namespace Tutorial9.Services;

public interface IDbService
{
    Task DoSomethingAsync();
    Task<int> ProcedureAsync(ProductWarehouseRequestDTO requestDto);
    Task<int> AddProductToWarehouseAsync(ProductWarehouseRequestDTO requestDto);
}