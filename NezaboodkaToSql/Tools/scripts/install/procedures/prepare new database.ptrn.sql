DELIMITER //
DROP PROCEDURE IF EXISTS _prepare_new_database //
CREATE PROCEDURE _prepare_new_database(
	IN db_name VARCHAR(64)
)
BEGIN
/*---------------------------------------/
			Schema Info
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- type
	"
		#include "prepare/type.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
	CALL QEXEC(CONCAT(	-- type_closure
	"
		#include "prepare/type_closure.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
	CALL QEXEC(CONCAT(	-- field
	"
		#include "prepare/field.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
	CALL QEXEC(CONCAT(	-- index base
	"
		#include "prepare/index/index_base.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
	CALL QEXEC(CONCAT(	-- index field
	"
		#include "prepare/index/index_field.sql" {
			'${db_name}': '", db_name, "'
		}
	"));

/*---------------------------------------/
			Default Structure
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- db_key
	"
		#include "prepare/db_key.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
	CALL QEXEC(CONCAT(	-- list
	"
		#include "prepare/list.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
	CALL QEXEC(CONCAT(	-- list_item
	"
		#include "prepare/list_item.sql" {
			'${db_name}': '", db_name, "'
		}
	"));
END //
