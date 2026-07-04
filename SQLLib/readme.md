Small library to help with MSSQL (low level)

Example:

``` cs

	using (SQLLib sql = new SQLLib())
	{
		sql.ApplicationName = "My application"; //will be visible, for example, in Activity Monitor in SSMS
		if (sql.ConnectDatabase("myserver.example.com", "my database", true, true) == false)
			return (null);

		SqlDataReader dr = sql.ExecSQLReader("select * from mylogtable where name=@name order by DT desc",
			new SQLParam("@name", "Foxy"));
			
		while(dr.Read())
		{
			Console.WriteLine(Convert.ToString(dr["Log"]));
		}
		
		dr.Close();
	}

```

