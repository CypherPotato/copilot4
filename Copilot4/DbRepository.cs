using CypherPotato.SqliteCollections;

namespace Copilot4;

class DbRepository<T> : SqliteRepository<T> where T : notnull {

    protected override SqliteList OpenRepository () {
        return SqliteList.Open ( Program.AppDatabase, $"Repository_{typeof ( T ).Name}" );
    }
}
