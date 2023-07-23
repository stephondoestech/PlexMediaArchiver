﻿using PMAData.Model;
using PMAData.Repositories.Abstract;
using Dapper;
using System.Linq;
using System.Collections.Generic;

namespace PMAData.Repositories
{
    public class TVShowRepository : BaseRepository, ITVShowRepository
    {
        public TVShowRepository(Database.DatabaseConfig databaseConfig, NLog.Logger logger) : base(databaseConfig, logger) { }

        public void CreateOrUpdate(TVShow tvshow)
        {
            DoCreateOrUpdate(tvshow);
        }

        private void DoCreateOrUpdate(TVShow tvshow, int retry = 0)
        {
            try
            {
                using (var conn = databaseConfig.NewConnection())
                {
                    var dbTVShow = conn.QueryFirstOrDefault<TVShow>($"SELECT * FROM TVShow WHERE ID = @TVShowID", new { TVShowID = tvshow.ID });

                    string sql = "";

                    if (dbTVShow != null)
                    {
                        sql = "UPDATE TVShow SET Title = @Title, Year = @Year, LastPlayed = @LastPlayed, Added = @Added, FileSize = @FileSize, IsCurrent = @isCurrent, IsArchived = @isArchived WHERE ID = @ID;";
                    }
                    else
                    {
                        sql = "INSERT INTO TVShow (ID, Title, Year, LastPlayed, Added, FileSize, IsCurrent, IsArchived) VALUES (@ID, @Title, @Year, @LastPlayed, @Added, @FileSize, @isCurrent, @isArchived);";
                    }

                    if (!string.IsNullOrEmpty(sql))
                    {
                        conn.Execute(sql, tvshow);
                    }

                    conn.Execute($"DELETE FROM GenericData WHERE ID = @MediaID AND MediaType = @MediaType", new { MediaID = tvshow.ID, MediaType = "tvshow" });

                    if (tvshow.GenericData.Any())
                    {
                        foreach (var generic in tvshow.GenericData)
                        {
                            sql = "INSERT INTO GenericData (ID, MediaID, MediaType, DataKey, DataValue) VALUES (@ID, @MediaID, @MediaType, @DataKey, @DataValue)";

                            conn.Execute(sql, generic);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, $"Error: {tvshow.Title}");

                if (ex.Message.Contains(" locked"))
                {
                    if (retry < 3)
                    {
                        logger.Info($"Retrying...");
                        DoCreateOrUpdate(tvshow, retry + 1);
                    }
                    else
                    {
                        logger.Error($"Fatal error: {tvshow.Title}");
                    }
                }
            }
        }

        public int GetCount()
        {
            if (!databaseConfig.HasConnection)
            {
                return 0;
            }

            try
            {
                return databaseConfig.Connection.Query<int>(@$"SELECT COUNT(1) FROM TVShow").FirstOrDefault();
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
                return -1;
            }
        }

        public List<TVShow> GetTVShows()
        {
            if (!databaseConfig.HasConnection)
            {
                return new List<TVShow>();
            }

            var tvShows = databaseConfig.Connection.Query<TVShow>($"SELECT * FROM TVShow").ToList();

            foreach (var tvShow in tvShows)
            {
                tvShow.GenericData = databaseConfig.Connection.Query<GenericData>("SELECT * FROM GenericData WHERE MediaID = @MediaID AND MediaType = 'tvshow'", new { MediaID = tvShow.ID }).ToList();
            }

            return tvShows;
        }
    }
}
