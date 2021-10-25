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
            {"plaintext": "ID", "type": "number", "cellStyle": {}},
            {"plaintext": "Command", "type": "string", "cellStyle": {}},
            {"plaintext": "Status", "type": "string", "cellStyle": {}},
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
                let jinfo = data[j];
                let row = {
                    "rowStyle": {},
                    "ID": {"plaintext": jinfo["id"], "cellStyle": {}},
                    "Command": {"plaintext": jinfo["command"], "cellStyle": {}},
                    "Status": {"plaintext": jinfo["status"], "cellStyle": {}},
                };
                rows.push(row);
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "Job List"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}