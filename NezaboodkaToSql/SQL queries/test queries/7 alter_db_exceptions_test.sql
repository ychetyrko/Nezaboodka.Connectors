/*********************************************

		Alter DB exceptions tests
			[No changes expected]

**********************************************/

USE `nz_admin_db`;

CALL before_alter_database_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('Parent', '_parent', ''),
('Child1', '_child1', 'Parent'),
('Child2', '_child2', 'Parent'),
('Child3', '_child3', 'Child1');
INSERT INTO `field_add_list`
(`owner_type_name`, `name`, `col_name`, `type_name`, `is_list`, `compare_options`)
VALUES
('Parent', 'Left', '_left', 'Parent', FALSE, 'IgnoreCase'),
('Child1', 'RightChild', '_right_child', 'Child2', FALSE, 'IgnoreCase');
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
	Add type with incorrect parent
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('Child4', '_child4', 'Parent'),
('NewType', '_new_type', 'IncorrectParent');	-- incorrect
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
	Add type with existing table name
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('Child4', '_child4', 'Parent'),
('NewType1', '_parent', 'Parent');	-- incorrect
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
	Add field with incorrect types
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_add_list`
(`owner_type_name`, `name`, `col_name`, `type_name`, `is_list`, `compare_options`)
VALUES
('Parent', 'Age', '_age', 'INT', FALSE, 'None'),
('Parent', 'Name', '_name', 'IncorrectType123~', FALSE, 'IgnoreCase');	-- incorrect
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Remove inexisting type
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('Child1'),
('InexistingType');	-- incorrect
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Remove inherited type
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('Parent'),	-- inherited by Child1
('Child2');
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Remove inexisting field
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Child1', 'RightChild'),
('Child1', 'InexistingFiled');
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Add duplicated field
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_add_list`
(`owner_type_name`, `name`, `col_name`, `type_name`, `is_list`, `compare_options`)
VALUES
('Child1', 'RightChild', '_right_child_new', 'Child3', FALSE, 'IgnoreCase');
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Add duplicated field
			(in base type)
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_add_list`
(`owner_type_name`, `name`, `col_name`, `type_name`, `is_list`, `compare_options`)
VALUES
('Child2', 'Left', '_left', 'INT', FALSE, 'None');	-- exists in 'Parent' type
CALL alter_database_schema('nz_test_db');

/*---------------------------------------/
		Add duplicated column
--------------------------------------*/
CALL before_alter_database_schema();
INSERT INTO `field_add_list`
(`owner_type_name`, `name`, `col_name`, `type_name`, `is_list`, `compare_options`)
VALUES
('Child1', 'NewChild', '_right_child', 'Child3', FALSE, 'IgnoreCase');
CALL alter_database_schema('nz_test_db');
