function(task, responses){
    if(task.status.includes("error")){
        const combined = responses.reduce( (prev, cur) => {
            return prev + cur;
        }, "");
        return {'plaintext': combined};
    }else if(responses.length > 0){
        let file = {};
        let data = "";
        let rows = [];
        let headers = [
            {"plaintext": "Path", "type": "string", "cellStyle": {"fillWidth": true}},
            {"plaintext": "Last Access Time", "type": "string", "cellStyle":  {}},
            {"plaintext": "Last Write Time", "type": "string", "cellStyle": {}},
            {"plaintext": "Creation Time", "type": "string", "cellStyle": {}},
        ];
        for(let i = 0; i < responses.length; i++)
        {
            console.log(responses+[i])
            try{
                data = JSON.parse(responses[i]);
            }catch(error){
               const combined = responses.reduce( (prev, cur) => {
                    return prev + cur;
                }, "");
                return {'plaintext': combined};
            }
            
            for(let j = 0; j < data.length; j++){
                let pinfo = data[j];
                let row = {
                    "rowStyle": {},
                    "Path": {"plaintext": pinfo["path"], "cellStyle": {"fillWidth": true}},
                    "Last Access Time": {"plaintext": pinfo["LastAccessTime"], "cellStyle": {}},
                    "Last Write Time": {"plaintext": pinfo["LastWriteTime"], "cellStyle": {}},
                    "Creation Time": {"plaintext": pinfo["CreationTime"], "cellStyle": {}},

                };
                rows.push(row);
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "Directory Listing"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}
