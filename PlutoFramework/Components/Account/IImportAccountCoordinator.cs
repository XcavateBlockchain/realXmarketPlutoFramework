namespace PlutoFramework.Components.Account;

public enum ImportAccountFlowMode
{
    Create,
    Import,
}

public interface IImportAccountCoordinator
{
    Task StartAsync(ImportAccountFlowMode flowMode);
}
