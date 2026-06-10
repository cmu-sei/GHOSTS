# Database Handler Configuration

The database handler is used to perform insert, query, and delete operations to one or more MySql database servers. The configuration can be used specify a simplified schema with multiple databases, each with multiple tables, each table with multiple columns.

When the handler is executed, a random database is chosen along with a random table from that database. This DB/Table is queried for row count, and if empty, 10 rows are generated as initial content.  If non-empty, either an insert operation (one row inserted), delete (first row is deleted), or query operation is performed (a maximum of `query-limit` rows are returned and written to the Ghosts log file).  The query starts at a random offset from within the rows.  If the `max-rows` parameter is non-zero then when this number of rows is reached a deletion operation is forced.

A sample timeline is shown below:

```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType":  "Database",
      "HandlerArgs": {
        "delay-jitter": 50,
        "insert-probability": 30,
        "delete-probability": 20,
        "query-probability": 50,
        "query-limit": 30,
        "max-rows": 100,
        "port": 3306,
        "DatabaseTargets": {
           "Version": "1.0",
             "Data": {
               "<db_key1>": {
                  "Username": "a_user",
                  "Password": "<Base64 Encoded Password>",
                  "Databases": [
                    {
                      "Name": "AcmeInc",
                      "Tables": [
                        {
                          "Name": "Employees",
                          "Columns": [
                            {
                              "Name": "Name",
                              "ContentHint": "name"
                            },
                            {
                              "Name": "Title",
                              "ContentHint": "title"
                            },
                            {
                              "Name": "SSN",
                              "ContentHint": "ssn"
                            },
                            {
                              "Name": "LastPerformanceRating",
                              "Range": "0,10"
                            },
                            {
                              "Name": "RemoteWorkEnabled",
                              "Choice": "yes,no,pending"
                            },
                            {
                              "Name": "Phone",
                              "ContentHint": "phone"
                            },
                            {
                              "Name": "Street",
                              "ContentHint": "street"
                            },
                            {
                              "Name": "City",
                              "ContentHint": "city"
                            }
                          ]
                        }
                      ]
                    }
                  ]

               }
              }
        }
      },
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": true,
      "TimeLineEvents": [
        {
          "Command": "random",
          "CommandArgs": [
             "<IP_of_target_db_key1>|<db_key1>",
          ],
          "DelayAfter": 20000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```

The `DatabaseTargets[Data]` entry in the `HandlerArgs` is a dictionary, with each key/value specifying a target MySQL database. The `Username`, `Password` specify the credentials used for database access, and the `Databases` entry is a list of simplified database schema that can be accessed. It is assumed that this schema exists on the MySql server and is assumed to have primary key named `id` as the first column that is an auto-incrementing integer.

Each entry in `Tables` has a list of `Columns` that describe the column entries. Each table is assumed to be comprised entirely of either VARCHAR(255) string data or INT data.

The `Column` entry can have a `ContentHint` with a string that identifies a category that is used from the default database corpus in Ghosts (a file named `database-content.csv`).  The supported `ContentHint` categories are:

- name
- city
- phone
- ssn
- street
- title
- uuid
- vin (Vehicle ID number)
- car (car make)
- plate (car license plate)

If an invalid category is given, the returned value will always be: `category + "_noValueAvailable"`

During row generation, a random value is chosen from the Ghosts database corpus based on the category name.

The `Column` entry supports a `Choice` key whose value is a comma-seperated list of string choices for the column; one is selected randomly during row generation.

The `Column` entry supports a `Range` key whose value is a comma-seperated list of two decimal integers (min, max), and a random value is chosen between min,max (not including max).  The database type for this column is an integer.

A `Column` entry should only have one of either `ContentHint`, `Choice` or `Range`.

The `TimeLineEvents[CommandArgs]` value is assumed to have a list of strings formatted as "<IP_of_target>|<db_key_of_target>" and each actuation cycle picks one of these at random.  Currently, the only supported value of  `TimeLineEvents[Command]`  is `random`.

