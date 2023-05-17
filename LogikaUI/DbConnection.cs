using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;

namespace LogikaUI;

public class DbConnection
{
    private static string connectionString = "Host=localhost;Username=skygrel19;Password=root;Database=logika";
    public static NpgsqlDataSource? DbDataSource;

    async public static Task<NpgsqlDataSource> GetDataSource()
    {
        if (DbDataSource == null)
        {
            DbDataSource = NpgsqlDataSource.Create(connectionString);
        }
        
        return DbDataSource;
    }
}