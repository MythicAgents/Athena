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
            {"plaintext": "name", "type": "string", "cellStyle": {}},
            {"plaintext": "value", "type": "string", "cellStyle": {}},
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
                    "name": {"plaintext": pinfo["Name"], "cellStyle": {}},
                    "value": {"plaintext": pinfo["Value"], "cellStyle": {}},
                };
                rows.push(row);
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "Environment Variables"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}
