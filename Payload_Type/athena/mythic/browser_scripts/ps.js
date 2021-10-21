function(task, response){
    var rows = [];
    console.log(response);
    for(var i = 0; i < response.length; i++){
      try{
          var data = JSON.parse(response[i]['response'].replace("'", '"'));
      }catch(error){
        var msg = "Unhandled exception in ps.js for " + task.command + " (ID: " + task.id + "): " + error;
        console.error(msg);
          return response[i]['response'];
      }
     var row_style = "";
     var cell_style = {"name": "max-width:0;",
     "user": "max-width:0;",
     "description": "max-width:0;",
     "company name": "max-width:0;",};
      for (var j = 0; j < data.length; j++)
      {
            function escapeHTML(content)
            {
                return content
                    .replace(/&/g, "&amp;")
                    .replace(/</g, "&lt;")
                    .replace(/>/g, "&gt;")
                    .replace(/"/g, "&quot;")
                    .replace(/'/g, "&#039;");
            }
            rows.push({"pid": data[j]['process_id'],
                       "name": data[j]["name"],
                       "arch": data[j]["title"],
                            "row-style": row_style,
                            "cell-style": cell_style
                        });
      }
    }
    var output = support_scripts['apollo_create_table']([{"name":"pid", "size":"30px"},{"name":"name", "size":"30px"},{"name":"title", "size":"60px"}], rows);
    return output;
  }