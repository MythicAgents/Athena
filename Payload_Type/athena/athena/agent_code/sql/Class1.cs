using System.Data.SqlClient;

namespace sql
{
    public class Class1
    {
        public void Test()
        {
            string connectionString = "your_connection_string";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sqlQuery = "SELECT * FROM YourTableName";

                using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader.GetString(0);
                            int anotherColumn = reader.GetInt32(1);

                            Console.WriteLine($"Column1: {columnName}, Column2: {anotherColumn}");
                        }
                    }
                }
            }
        }
    }
}