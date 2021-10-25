function(task, responses){
    console.log("Running");
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
            {"plaintext": "path", "type": "number", "cellStyle": {}},
            {"plaintext": "LastAccessTime", "type": "string", "cellStyle": {}},
            {"plaintext": "LastWriteTime", "type": "string", "cellStyle": {}},
            {"plaintext": "CreationTime", "type": "string", "cellStyle": {}},
        ];
        for(let i = 0; i < responses.length; i++)
        {
            try{
                data = JSON.parse(responses[i]);
            }catch(error){
                console.log(error);
               const combined = responses.reduce( (prev, cur) => {
                    return prev + cur;
                }, "");
                return {'plaintext': combined};
            }
            
            for(let j = 0; j < data.length; j++){
                let dinfo = data[j];
                let row = {
                    "rowStyle": {},
                    "path": {"plaintext": dinfo["path"], "cellStyle": {}},
                    "LastAccessTime": {"plaintext": dinfo["LastAccessTime"], "cellStyle": {}},
                    "LastWriteTime": {"plaintext": dinfo["LastWriteTime"], "cellStyle": {}},
                    "CreationTime": {"plaintext": dinfo["CreationTime"], "cellStyle": {}},
                };
                rows.push(row);
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "File List"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}