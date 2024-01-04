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
            {"plaintext": "DriveName", "type": "string", "cellStyle": {}},
            {"plaintext": "DriveType", "type": "string", "cellStyle":  {}},
            {"plaintext": "FreeSpace (GB)", "type": "string", "cellStyle": {}},
            {"plaintext": "TotalSpace (GB)", "type": "string", "cellStyle": {}},
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
                    "DriveName": {"plaintext": pinfo["DriveName"], "cellStyle": {}},
                    "DriveType": {"plaintext": pinfo["DriveType"], "cellStyle": {}},
                    "FreeSpace (GB)": {"plaintext": pinfo["FreeSpace"], "cellStyle": {}},
                    "TotalSpace (GB)": {"plaintext": pinfo["TotalSpace"], "cellStyle": {}},

                };
                rows.push(row);
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "Drives"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}
