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
            {"plaintext": "id", "type": "number", "cellStyle": {}},
            {"plaintext": "name", "type": "string", "cellStyle": {}},
            {"plaintext": "title", "type": "string", "cellStyle": {}},
        ];
        for(let i = 0; i < responses.length; i++)
        {
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
                    "id": {"plaintext": pinfo["process_id"], "cellStyle": {}},
                    "name": {"plaintext": pinfo["name"], "cellStyle": {}},
                    "title": {"plaintext": pinfo["title"], "cellStyle": {}},
                };
                rows.push(row);
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "Process List"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}